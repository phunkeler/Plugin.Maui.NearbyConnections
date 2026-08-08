namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Device-state projection and dispatcher marshalling for <see cref="NearbyConnectionsImplementation"/>.
/// Everything that mutates <c>_devices</c>, writes a <see cref="NearbyDevice"/> property, or raises
/// a lifecycle event lives here and runs on the dispatcher.
/// </summary>
sealed partial class NearbyConnectionsImplementation
{
    /// <summary>
    /// Runs <paramref name="action"/> on the UI dispatcher, or inline when no dispatcher is
    /// available (unit tests and the <c>net10.0</c> target, where there is no UI thread to marshal
    /// to). <see cref="IDispatcher.IsDispatchRequired"/> keeps the already-on-UI-thread case
    /// synchronous rather than posting a needless continuation.
    /// </summary>
    async Task DispatchAsync(Action action)
    {
        if (_dispatcher is null || !_dispatcher.IsDispatchRequired)
        {
            action();
            return;
        }

        await _dispatcher.DispatchAsync(action).ConfigureAwait(false);
    }

    /// <summary>
    /// The one place a device's state changes. Every transition goes through here so there is a
    /// single site to log, break on, or extend — the compound invariant that used to span three
    /// properties is now one write, and this is where it happens.
    /// </summary>
    /// <remarks>
    /// The two transitions nested inside a larger dispatcher action
    /// (<see cref="OnRequestReceivedAsync"/>, <see cref="OnConnectedAsync"/>) assign inline instead,
    /// so the state change and the event raise stay in one dispatcher turn rather than splitting
    /// across two.
    /// </remarks>
    Task TransitionAsync(NearbyDevice device, DeviceState state)
        => DispatchAsync(() => device.State = state);

    /// <summary>
    /// Maps a handshake failure to the reason it is reported as. The exception type already carries
    /// the distinction, so nothing needs to be plumbed up from the platform layer:
    /// <see cref="NearbyConnectionTimeoutException"/> is thrown only when the plugin's own
    /// invitation deadline fired and the caller's token was not cancelled.
    /// </summary>
    static EndReason ReasonFor(Exception exception) => exception switch
    {
        OperationCanceledException => EndReason.Cancelled,
        NearbyConnectionTimeoutException => EndReason.TimedOut,
        _ => EndReason.Failed,
    };

    async Task PumpAdvertiseAsync(IAsyncEnumerable<NearbyConnectionRequest> stream, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await OnRequestReceivedAsync(request).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit — StopAdvertisingAsync cancelled the pump.
        }
        catch (Exception ex)
        {
            // A start failure surfaces here, not from StartAdvertisingAsync: the platform start
            // lives inside the enumerable. Swallowing this silently is exactly the failure mode
            // that has already cost a debugging session, so it is always logged.
            LogAdvertisePumpFailed(ex);
            await DispatchAsync(() => IsAdvertising = false).ConfigureAwait(false);
        }
    }

    async Task PumpDiscoverAsync(IAsyncEnumerable<NearbyDeviceEvent> stream, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var deviceEvent in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await OnDeviceEventAsync(deviceEvent).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit — StopDiscoveringAsync cancelled the pump.
        }
        catch (Exception ex)
        {
            LogDiscoverPumpFailed(ex);
            await DispatchAsync(() => IsDiscovering = false).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A device came into view. Adds it if new; a device already present (connected, or rediscovered
    /// after a drop) keeps its identity and status — the registry hands back the same instance, so
    /// re-adding would duplicate the row.
    /// </summary>
    async Task OnDeviceEventAsync(NearbyDeviceEvent deviceEvent)
    {
        var device = deviceEvent.Device;

        switch (deviceEvent.Type)
        {
            case NearbyDeviceEventType.Found:
                await DispatchAsync(() =>
                {
                    if (!_devices.Contains(device))
                    {
                        _devices.Add(device);
                    }
                }).ConfigureAwait(false);
                break;

            case NearbyDeviceEventType.Lost:
                await DispatchAsync(() =>
                {
                    // A connected device that goes out of discovery range is still connected —
                    // dropping it here would delete a live conversation from the UI. Only remove
                    // devices that are merely visible.
                    if (device.Status is NearbyDeviceStatus.Visible)
                    {
                        _devices.Remove(device);
                    }
                }).ConfigureAwait(false);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// An inbound request arrived. The device is surfaced in <see cref="Devices"/> before the event
    /// is raised, so a handler that inspects the collection sees a consistent picture.
    /// </summary>
    async Task OnRequestReceivedAsync(NearbyConnectionRequest request)
    {
        var device = request.RemoteDevice;
        _pendingRequests[device.Id] = request;

        await DispatchAsync(() =>
        {
            if (!_devices.Contains(device))
            {
                _devices.Add(device);
            }

            // No role here: the local device is not an acceptor until AcceptAsync is called.
            device.State = new DeviceState.RequestReceived();

            RaiseConnectionRequested(device);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// A connection was established, from either side. Publishes the connection onto the device,
    /// raises <see cref="INearbyConnections.ConnectionEstablished"/>, and arms the drop notification.
    /// </summary>
    async Task OnConnectedAsync(NearbyDevice device, NearbyConnection connection, ConnectionRole role)
    {
        // Captured before raising: a handler could subscribe as a side effect, which would mask the
        // very condition being detected.
        var hasSubscribers = ConnectionEstablished is not null;

        await DispatchAsync(() =>
        {
            if (!_devices.Contains(device))
            {
                _devices.Add(device);
            }

            device.State = new DeviceState.Connected(role, connection);

            RaiseConnectionEstablished(device, connection);
        }).ConfigureAwait(false);

        if (!hasSubscribers)
        {
            LogNoConnectionEstablishedSubscribers(device.Id);
        }

        // One watcher per connection, regardless of which side disconnects, so ConnectionDropped is
        // raised exactly once from a single place. Fire-and-forget by design: the continuation is
        // the notification. Exceptions are handled inside WatchDisconnectAsync.
        _ = WatchDisconnectAsync(device, connection);
    }

    /// <summary>
    /// Awaits the connection's own disconnect signal and projects it into device state plus
    /// <see cref="INearbyConnections.ConnectionDropped"/>.
    /// </summary>
    async Task WatchDisconnectAsync(NearbyDevice device, NearbyConnection connection)
    {
        try
        {
            await connection.Disconnected.ConfigureAwait(false);

            await DispatchAsync(() =>
            {
                // Guard against a reconnect having already replaced the connection: only clear
                // state that still belongs to the connection that dropped. ReferenceEquals, not
                // record equality — two states could compare equal while holding the same
                // connection reference, and identity is what matters here.
                if (device.State is not DeviceState.Connected current
                    || !ReferenceEquals(current.Connection, connection))
                {
                    return;
                }

                device.State = new DeviceState.Visible();

                RaiseConnectionDropped(device, connection, EndReason.Disconnected);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogDisconnectWatchFailed(device.Id, ex);
        }
    }

    /// <summary>
    /// Returns a device to <see cref="DeviceState.Visible"/> after a failed, cancelled, or rejected
    /// handshake.
    /// </summary>
    /// <remarks>
    /// No connection was ever established on these paths, so there is nothing to raise
    /// <see cref="INearbyConnections.ConnectionDropped"/> about. The reason reaches the caller as
    /// the exception that <see cref="ConnectAsync"/> or <see cref="AcceptAsync"/> rethrows.
    /// </remarks>
    Task ResetToVisibleAsync(NearbyDevice device)
        => TransitionAsync(device, new DeviceState.Visible());

    /// <summary>
    /// Starts <paramref name="pump"/> and publishes its flag. Caller must hold <c>_stateGate</c>.
    /// </summary>
    /// <remarks>
    /// The flag is set before the pump task is created: a start failure surfaces inside the pump
    /// (the platform start lives in the enumerable), which clears the flag again, and that must not
    /// race ahead of the write that sets it.
    /// </remarks>
    static async Task StartPumpAsync(PumpState pump)
    {
        var cts = new CancellationTokenSource();

        pump.Cts = cts;

        await pump.SetFlag(true).ConfigureAwait(false);

        pump.Task = pump.Start(cts.Token);
    }

    /// <summary>
    /// Cancels <paramref name="pump"/>, waits for it to drain, and clears its flag. Caller must
    /// hold <c>_stateGate</c>.
    /// </summary>
    static async Task StopPumpAsync(PumpState pump)
    {
        var cts = pump.Cts;
        var task = pump.Task;

        pump.Cts = null;
        pump.Task = null;

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (task is not null)
        {
            // The pump swallows cancellation, so this completes rather than throwing. Awaiting it
            // means the platform has actually stopped by the time this returns.
            await task.ConfigureAwait(false);
        }

        cts?.Dispose();

        await pump.SetFlag(false).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes devices that are merely visible, leaving connected ones in place.
    /// </summary>
    Task RemoveVisibleDevicesAsync()
        => DispatchAsync(() =>
        {
            for (var i = _devices.Count - 1; i >= 0; i--)
            {
                if (_devices[i].Status is NearbyDeviceStatus.Visible)
                {
                    _devices.RemoveAt(i);
                }
            }
        });

    /// <summary>
    /// One of the session's two background pumps: the stream-draining task, the source that stops
    /// it, and the flag it publishes.
    /// </summary>
    /// <param name="start">Starts the pump task for the supplied cancellation token.</param>
    /// <param name="setFlag">
    /// Publishes <see cref="INearbyConnections.IsAdvertising"/> or
    /// <see cref="INearbyConnections.IsDiscovering"/> on the dispatcher.
    /// </param>
    sealed class PumpState(Func<CancellationToken, Task> start, Func<bool, Task> setFlag)
    {
        public Func<CancellationToken, Task> Start { get; } = start;

        public Func<bool, Task> SetFlag { get; } = setFlag;

        public CancellationTokenSource? Cts { get; set; }

        public Task? Task { get; set; }
    }

    /// <summary>
    /// Raises a lifecycle event. A throwing handler must not take down the platform callback thread
    /// or starve the handlers after it, so every raise goes through here.
    /// </summary>
    /// <param name="handler">The event to raise, or <see langword="null"/> if nobody subscribed.</param>
    /// <param name="args">The arguments to raise it with.</param>
    /// <param name="eventName">The event's name, used only to attribute a handler failure in the log.</param>
    void Raise<TArgs>(EventHandler<TArgs>? handler, TArgs args, string eventName)
        where TArgs : EventArgs
    {
        try
        {
            handler?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            LogEventHandlerFailed(eventName, ex);
        }
    }

    void RaiseConnectionRequested(NearbyDevice device)
        => Raise(ConnectionRequested, new NearbyConnectionRequestedEventArgs(device), nameof(ConnectionRequested));

    void RaiseConnectionEstablished(NearbyDevice device, NearbyConnection connection)
        => Raise(ConnectionEstablished, new NearbyConnectionChangedEventArgs(device, connection), nameof(ConnectionEstablished));

    void RaiseConnectionDropped(NearbyDevice device, NearbyConnection connection, EndReason reason)
        => Raise(ConnectionDropped, new NearbyConnectionChangedEventArgs(device, connection, reason), nameof(ConnectionDropped));
}
