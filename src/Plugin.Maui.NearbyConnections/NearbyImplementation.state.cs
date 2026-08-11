namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Device-state projection for <see cref="NearbyImplementation"/>. Everything that records what a
/// platform callback reported lives here.
/// </summary>
/// <remarks>
/// Nothing in this file marshals to a UI thread. <see cref="NearbyDeviceRegistry"/> is thread-safe
/// by construction, so a platform callback records what it saw on whatever thread it arrived on and
/// the change is fanned out from there. Consumers that need a UI thread apply that themselves —
/// see <see cref="NearbyDeviceCollection"/>.
/// </remarks>
sealed partial class NearbyImplementation
{
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

    /// <summary>
    /// The one place a device's state changes. Every transition goes through here so there is a
    /// single site to log, break on, or extend.
    /// </summary>
    /// <remarks>
    /// A no-op when the device is unknown to the session: a transition for a device that was never
    /// added, or has since been removed, has nothing to update.
    /// </remarks>
    void Transition(NearbyDevice device, NearbyDeviceStatus status, ConnectionRole? role)
        => _registry.Update(
            device.Id,
            current => current.Status == status && current.Role == role
                ? current
                : current with { Status = status, Role = role });

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
            LogAdvertisePumpFailed(ex);
            IsAdvertising = false;
        }
    }

    async Task PumpDiscoverAsync(IAsyncEnumerable<NearbyDeviceEvent> stream, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var deviceEvent in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                OnDeviceEvent(deviceEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit — StopDiscoveryAsync cancelled the pump.
        }
        catch (Exception ex)
        {
            LogDiscoverPumpFailed(ex);
            IsDiscovering = false;
        }
    }

    /// <summary>
    /// A device came into view, or went out of it.
    /// </summary>
    void OnDeviceEvent(NearbyDeviceEvent deviceEvent)
    {
        var device = deviceEvent.Device;

        switch (deviceEvent.Type)
        {
            case NearbyDeviceEventType.Found:
                _registry.AddIfAbsent(device);
                break;

            case NearbyDeviceEventType.Lost:
                // A connected device that goes out of discovery range is still connected — dropping
                // it here would delete a live conversation from the UI. Only remove devices that
                // are merely visible.
                if (_registry.TryGet(device.Id, out var known)
                    && known.Status is NearbyDeviceStatus.Visible)
                {
                    _registry.Remove(device.Id);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// An inbound request arrived. The device is surfaced before its status changes, so a consumer
    /// watching <see cref="INearbyDevices.Changes"/> sees the device appear and then transition
    /// rather than a status change for a device it has never heard of.
    /// </summary>
    async Task OnRequestReceivedAsync(NearbyConnectionRequest request)
    {
        var device = request.RemoteDevice;

        if (_options.AutoAcceptConnectionRequests)
        {
            await AutoAcceptAsync(request, device).ConfigureAwait(false);
            return;
        }

        _pendingRequests[device.Id] = request;

        _registry.AddIfAbsent(device);

        Transition(device, NearbyDeviceStatus.RequestReceived, role: null);
    }

    /// <summary>
    /// Answers an inbound request on the application's behalf when
    /// <see cref="NearbyOptions.AutoAcceptConnectionRequests"/> is set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The request is never published to <c>_pendingRequests</c>, so
    /// <see cref="NearbyDeviceStatus.RequestReceived"/> is not observable in this mode. The device
    /// is surfaced and moved to <see cref="NearbyDeviceStatus.Connecting"/> before the platform
    /// call, so a consumer watching device changes sees the same progression as an outbound
    /// connection rather than a row that appears already connected.
    /// </para>
    /// <para>
    /// A failure here resets the device to <see cref="NearbyDeviceStatus.Visible"/> exactly as the
    /// manual <see cref="AcceptAsync"/> path does. There is no caller to rethrow to — no application
    /// code asked for this accept — so the exception is logged and swallowed rather than escaping
    /// into the advertise pump, where it would tear down advertising for one failed handshake.
    /// </para>
    /// </remarks>
    async Task AutoAcceptAsync(NearbyConnectionRequest request, NearbyDevice device)
    {
        _registry.AddIfAbsent(device);
        Transition(device, NearbyDeviceStatus.Connecting, ConnectionRole.Acceptor);

        try
        {
            var connection = await request.AcceptAsync(CancellationToken.None).ConfigureAwait(false);
            OnConnected(device, connection, ConnectionRole.Acceptor);
        }
        catch (Exception ex)
        {
            var reason = ReasonFor(ex);
            LogHandshakeEnded(device.Id, reason);
            LogAutoAcceptFailed(device.Id, ex);
            ResetToVisible(device);
        }
    }

    /// <summary>
    /// A connection was established, from either side. Publishes the connection, moves the device to
    /// <see cref="NearbyDeviceStatus.Connected"/>, and arms the drop notification.
    /// </summary>
    void OnConnected(NearbyDevice device, NearbyConnection connection, ConnectionRole role)
    {
        // Published before the status change: a consumer that reacts to Connected by looking the
        // connection up must never lose that race.
        _activeConnections[device.Id] = connection;

        _registry.AddIfAbsent(device);
        Transition(device, NearbyDeviceStatus.Connected, role);

        // One watcher per connection, regardless of which side disconnects, so the drop is recorded
        // exactly once from a single place. Fire-and-forget by design: the continuation is the
        // notification. Exceptions are handled inside WatchDisconnectAsync.
        _ = WatchDisconnectAsync(device, connection);
    }

    /// <summary>
    /// Awaits the connection's own disconnect signal and projects it into device state.
    /// </summary>
    async Task WatchDisconnectAsync(NearbyDevice device, NearbyConnection connection)
    {
        try
        {
            await connection.Disconnected.ConfigureAwait(false);

            // Guard against a reconnect having already replaced the connection: only clear the
            // entry that still belongs to the connection that dropped. This overload compares the
            // value too, and does so atomically, so the check and the removal cannot interleave
            // with a reconnect. Losing the race means a newer connection owns the device now and
            // there is nothing to report.
            if (!_activeConnections.TryRemove(
                    new KeyValuePair<string, NearbyConnection>(device.Id, connection)))
            {
                return;
            }

            ResetToVisible(device);
        }
        catch (Exception ex)
        {
            LogDisconnectWatchFailed(device.Id, ex);
        }
    }

    /// <summary>
    /// Returns a device to <see cref="NearbyDeviceStatus.Visible"/> after a dropped connection, or a
    /// failed, cancelled, or rejected handshake.
    /// </summary>
    void ResetToVisible(NearbyDevice device)
        => Transition(device, NearbyDeviceStatus.Visible, role: null);

    /// <summary>
    /// Starts the discovery refresh loop, if the options ask for one. Caller must hold
    /// <c>_stateGate</c>.
    /// </summary>
    void StartRefreshLoop()
    {
        if (_options.DiscoveryRefreshInterval is not { } interval)
        {
            return;
        }

        var cts = new CancellationTokenSource();

        _refreshCts = cts;
        _refreshTask = RefreshDiscoveryLoopAsync(interval, cts.Token);
    }

    /// <summary>
    /// Signals the refresh loop to stop. Callable with or without <c>_stateGate</c> held, and does
    /// not wait — see <see cref="DrainRefreshLoopAsync"/> for why the two halves are separate.
    /// </summary>
    void CancelRefreshLoop() => _refreshCts?.Cancel();

    /// <summary>
    /// Waits for a cancelled refresh loop to finish and clears its state. Caller must
    /// <b>not</b> hold <c>_stateGate</c>.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="CancelRefreshLoop"/> to avoid a deadlock: the loop body takes
    /// <c>_stateGate</c> to restart the pump, so awaiting it while holding that gate would wait
    /// forever on a loop that cannot proceed. Callers cancel before taking the gate and drain after
    /// releasing it.
    /// </remarks>
    async Task DrainRefreshLoopAsync()
    {
        var cts = _refreshCts;
        var task = _refreshTask;

        _refreshCts = null;
        _refreshTask = null;

        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }

        cts?.Dispose();
    }

    /// <summary>
    /// Restarts discovery on <paramref name="interval"/>, removing devices that the new pass did
    /// not re-report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A restart, rather than a timestamp sweep, because both platforms report discovery on an edge:
    /// <c>onEndpointFound</c> and <c>foundPeer</c> fire when a device appears and never again while
    /// it stays put. Elapsed silence therefore carries no information about presence, and the only
    /// way to learn what is still in range is to ask again.
    /// </para>
    /// <para>
    /// The restart runs under <c>_stateGate</c>, so it cannot interleave with a caller's own
    /// start/stop, and it re-checks <see cref="IsDiscovering"/> after acquiring the gate — a
    /// <c>StopDiscoveryAsync</c> may have won the race while this was waiting.
    /// </para>
    /// </remarks>
    async Task RefreshDiscoveryLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    if (!IsDiscovering)
                    {
                        return;
                    }

                    _registry.BeginGeneration();

                    await StopPumpAsync(_discover).ConfigureAwait(false);
                    StartPump(_discover);
                }
                finally
                {
                    _stateGate.Release();
                }

                // Outside the gate: the platform re-reports through the discovery pump, which needs
                // the gate-free window to deliver. Devices still absent when the next tick arrives
                // are evicted by that tick's BeginGeneration/EvictUnconfirmed pair.
                await EvictAfterSettleAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Discovery stopped, or the session was disposed.
        }
        catch (Exception ex)
        {
            LogRefreshDiscoveryFailed(ex);
        }
    }

    /// <summary>
    /// Gives a freshly restarted discovery pass a moment to re-report what is in range, then drops
    /// whatever it did not.
    /// </summary>
    /// <remarks>
    /// The settle window is a heuristic, and deliberately a generous fraction of the refresh
    /// interval rather than a fixed constant: a device that is present but slow to be re-reported
    /// must not be evicted and immediately re-added, which would make a bound row flicker.
    /// </remarks>
    async Task EvictAfterSettleAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(RefreshSettleWindow, _timeProvider, cancellationToken).ConfigureAwait(false);

        _registry.EvictUnconfirmed();
    }

    /// <summary>
    /// Starts <paramref name="pump"/> and publishes its flag. Caller must hold <c>_stateGate</c>.
    /// </summary>
    /// <remarks>
    /// The flag is set before the pump task is created: a start failure surfaces inside the pump
    /// (the platform start lives in the enumerable), which clears the flag again, and that must not
    /// race ahead of the write that sets it.
    /// </remarks>
    static void StartPump(PumpState pump)
    {
        var cts = new CancellationTokenSource();
        pump.Cts = cts;
        pump.SetFlag(true);
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

        pump.SetFlag(false);
    }

    /// <summary>
    /// One of the session's two background pumps: the stream-draining task, the source that stops
    /// it, and the flag it publishes.
    /// </summary>
    /// <param name="start">Starts the pump task for the supplied cancellation token.</param>
    /// <param name="setFlag">
    /// Publishes <see cref="INearby.IsAdvertising"/> or <see cref="INearby.IsDiscovering"/>.
    /// </param>
    sealed class PumpState(Func<CancellationToken, Task> start, Action<bool> setFlag)
    {
        public Func<CancellationToken, Task> Start { get; } = start;

        public Action<bool> SetFlag { get; } = setFlag;

        public CancellationTokenSource? Cts { get; set; }

        public Task? Task { get; set; }
    }
}
