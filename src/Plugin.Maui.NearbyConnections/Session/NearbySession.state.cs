namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Device-state projection and dispatcher marshalling for <see cref="NearbySession"/>.
/// Everything that mutates <c>_devices</c>, writes a <see cref="NearbyDevice"/> property, or raises
/// a lifecycle event lives here and runs on the dispatcher.
/// </summary>
sealed partial class NearbySession
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

            device.Role = ConnectionRole.Acceptor;
            device.Status = NearbyDeviceStatus.RequestReceived;

            RaiseConnectionRequested(device);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// A connection was established, from either side. Publishes the connection onto the device,
    /// raises <see cref="INearbySession.ConnectionEstablished"/>, and arms the drop notification.
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

            device.Role = role;
            device.Connection = connection;
            device.Status = NearbyDeviceStatus.Connected;

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
    /// <see cref="INearbySession.ConnectionDropped"/>.
    /// </summary>
    async Task WatchDisconnectAsync(NearbyDevice device, NearbyConnection connection)
    {
        try
        {
            await connection.Disconnected.ConfigureAwait(false);

            await DispatchAsync(() =>
            {
                // Guard against a reconnect having already replaced the connection: only clear
                // state that still belongs to the connection that dropped.
                if (!ReferenceEquals(device.Connection, connection))
                {
                    return;
                }

                device.Connection = null;
                device.Role = null;
                device.Status = NearbyDeviceStatus.Visible;

                RaiseConnectionDropped(device, connection);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogDisconnectWatchFailed(device.Id, ex);
        }
    }

    /// <summary>
    /// Returns a device to <see cref="NearbyDeviceStatus.Visible"/> after a failed, cancelled, or
    /// rejected handshake.
    /// </summary>
    Task ResetToVisibleAsync(NearbyDevice device)
        => DispatchAsync(() =>
        {
            device.Connection = null;
            device.Role = null;
            device.Status = NearbyDeviceStatus.Visible;
        });

    Task SetIsAdvertisingAsync(bool value)
        => DispatchAsync(() => IsAdvertising = value);

    Task SetIsDiscoveringAsync(bool value)
        => DispatchAsync(() => IsDiscovering = value);

    /// <summary>
    /// Stops advertising and waits for the pump to drain. Caller must hold <c>_stateGate</c>.
    /// </summary>
    async Task StopAdvertisingCoreAsync()
    {
        var cts = _advertiseCts;
        var pump = _advertisePump;

        _advertiseCts = null;
        _advertisePump = null;

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (pump is not null)
        {
            // The pump swallows cancellation, so this completes rather than throwing. Awaiting it
            // means advertising has actually stopped by the time this returns.
            await pump.ConfigureAwait(false);
        }

        cts?.Dispose();

        await SetIsAdvertisingAsync(false).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops discovery and waits for the pump to drain. Caller must hold <c>_stateGate</c>.
    /// </summary>
    async Task StopDiscoveringCoreAsync()
    {
        var cts = _discoverCts;
        var pump = _discoverPump;

        _discoverCts = null;
        _discoverPump = null;

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (pump is not null)
        {
            await pump.ConfigureAwait(false);
        }

        cts?.Dispose();

        await SetIsDiscoveringAsync(false).ConfigureAwait(false);

        // Devices that were only ever visible are no longer meaningful once discovery stops.
        // Connected devices stay: stopping discovery does not end a conversation.
        await DispatchAsync(() =>
        {
            for (var i = _devices.Count - 1; i >= 0; i--)
            {
                if (_devices[i].Status is NearbyDeviceStatus.Visible)
                {
                    _devices.RemoveAt(i);
                }
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Raises <see cref="INearbySession.ConnectionRequested"/>. A throwing handler must not take
    /// down the platform callback thread or starve the handlers after it.
    /// </summary>
    void RaiseConnectionRequested(NearbyDevice device)
    {
        try
        {
            ConnectionRequested?.Invoke(this, new NearbyConnectionRequestedEventArgs(device));
        }
        catch (Exception ex)
        {
            LogEventHandlerFailed(nameof(ConnectionRequested), ex);
        }
    }

    void RaiseConnectionEstablished(NearbyDevice device, NearbyConnection connection)
    {
        try
        {
            ConnectionEstablished?.Invoke(this, new NearbyConnectionChangedEventArgs(device, connection));
        }
        catch (Exception ex)
        {
            LogEventHandlerFailed(nameof(ConnectionEstablished), ex);
        }
    }

    void RaiseConnectionDropped(NearbyDevice device, NearbyConnection connection)
    {
        try
        {
            ConnectionDropped?.Invoke(this, new NearbyConnectionChangedEventArgs(device, connection));
        }
        catch (Exception ex)
        {
            LogEventHandlerFailed(nameof(ConnectionDropped), ex);
        }
    }
}
