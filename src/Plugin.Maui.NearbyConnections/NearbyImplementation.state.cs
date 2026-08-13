namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyImplementation
{
    static EndReason ReasonFor(Exception exception) => exception switch
    {
        OperationCanceledException => EndReason.Cancelled,
        NearbyConnectionTimeoutException => EndReason.TimedOut,
        _ => EndReason.Failed,
    };

    void Transition(NearbyDevice device, NearbyDeviceStatus status, ConnectionRole? role)
        => _registry.Update(
            device.Id,
            current => current.Status == status && current.Role == role
                ? current
                : current with { Status = status, Role = role });

    async Task PumpAdvertiseAsync(
        IAsyncEnumerable<NearbyConnectionRequest> stream,
        TaskCompletionSource started,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await OnRequestReceivedAsync(request).ConfigureAwait(false);
            }

            started.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            started.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            IsAdvertising = false;

            if (!started.TrySetException(ex))
            {
                LogAdvertisePumpFailed(ex);
            }
        }
    }

    async Task PumpDiscoverAsync(
        IAsyncEnumerable<NearbyDeviceEvent> stream,
        TaskCompletionSource started,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var deviceEvent in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                OnDeviceEvent(deviceEvent);
            }

            started.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            started.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            IsDiscovering = false;

            if (!started.TrySetException(ex))
            {
                LogDiscoverPumpFailed(ex);
            }
        }
    }

    void OnDeviceEvent(NearbyDeviceEvent deviceEvent)
    {
        var device = deviceEvent.Device;

        switch (deviceEvent.Type)
        {
            case NearbyDeviceEventType.Found:
                _registry.AddIfAbsent(device);
                break;

            case NearbyDeviceEventType.Lost:
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

    void OnConnected(NearbyDevice device, NearbyConnection connection, ConnectionRole role)
    {
        _activeConnections[device.Id] = connection;
        _registry.AddIfAbsent(device);
        Transition(device, NearbyDeviceStatus.Connected, role);
        _ = WatchDisconnectAsync(device, connection);
    }

    async Task WatchDisconnectAsync(NearbyDevice device, NearbyConnection connection)
    {
        try
        {
            await connection.Disconnected.ConfigureAwait(false);

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

    void ResetToVisible(NearbyDevice device)
        => Transition(device, NearbyDeviceStatus.Visible, role: null);

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

                    var started = StartPump(_discover);

                    try
                    {
                        await started.Task.ConfigureAwait(false);
                    }
                    catch
                    {
                        // Clean up the failed pump so a later StartDiscoveryAsync call starts fresh
                        // rather than reusing a dead task/cts pair — same cleanup as the top-level
                        // Start*Async methods perform on the same failure shape.
                        await StopPumpAsync(_discover).ConfigureAwait(false);
                        throw;
                    }
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
        await Task.Delay(s_refreshSettleWindow, _timeProvider, cancellationToken).ConfigureAwait(false);

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
    /// <returns>
    /// A <see cref="TaskCompletionSource"/> whose task resolves once the platform start phase is
    /// known — see <see cref="IPlatformNearby"/>'s error-delivery remarks. Await
    /// <c>started.Task</c> to observe a start failure as a thrown exception.
    /// </returns>
    static TaskCompletionSource StartPump(PumpState pump)
    {
        var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        pump.Cts = cts;
        pump.SetFlag(true);
        pump.Task = pump.Start(started, cts.Token);

        return started;
    }

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
            await task.ConfigureAwait(false);
        }

        cts?.Dispose();
        pump.SetFlag(false);
    }

    sealed class PumpState(Func<TaskCompletionSource, CancellationToken, Task> start, Action<bool> setFlag)
    {
        public Func<TaskCompletionSource, CancellationToken, Task> Start { get; } = start;

        public Action<bool> SetFlag { get; } = setFlag;

        public CancellationTokenSource? Cts { get; set; }

        public Task? Task { get; set; }
    }
}
