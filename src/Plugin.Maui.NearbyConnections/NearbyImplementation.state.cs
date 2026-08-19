namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyImplementation
{
    static EndReason ReasonFor(Exception exception) => exception switch
    {
        OperationCanceledException => EndReason.Cancelled,
        NearbyConnectionTimeoutException => EndReason.TimedOut,
        _ => EndReason.Failed,
    };

    void Transition(
        NearbyDevice device,
        NearbyDeviceStatus status,
        ConnectionRole? role,
        DateTimeOffset? requestExpiresAt = null)
        => _registry.Update(
            device.Id,
            current => current.Status == status
                    && current.Role == role
                    && current.RequestExpiresAt == requestExpiresAt
                ? current
                : current with
                {
                    Status = status,
                    Role = role,
                    RequestExpiresAt = requestExpiresAt,
                });

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

        if (deviceEvent.Found)
        {
            _registry.AddIfAbsent(device);
            return;
        }

        if (_activeConnections.ContainsKey(device.Id)
            || _pendingRequests.ContainsKey(device.Id))
        {
            return;
        }

        if (_registry.TryGet(device.Id, out var known)
            && known.Status is NearbyDeviceStatus.Visible)
        {
            _registry.Remove(device.Id);
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

        var expiresAt = ArmRequestExpiry(device);

        _pendingRequests[device.Id] = request;

        _registry.AddIfAbsent(device);

        Transition(device, NearbyDeviceStatus.RequestReceived, role: null, expiresAt);
    }

    DateTimeOffset? ArmRequestExpiry(NearbyDevice device)
    {
        var timeout = _options.InboundRequestTimeout;

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return null;
        }

        var cts = new CancellationTokenSource();

        _requestExpiries[device.Id] = cts;
        _ = ExpireRequestAfterAsync(device, timeout, cts.Token);

        return _timeProvider.GetUtcNow() + timeout;
    }

    void DisarmRequestExpiry(string deviceId)
    {
        if (!_requestExpiries.TryRemove(deviceId, out var cts))
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    async Task ExpireRequestAfterAsync(NearbyDevice device, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(timeout, _timeProvider, cancellationToken).ConfigureAwait(false);

            if (!_pendingRequests.TryRemove(device.Id, out var request))
            {
                return;
            }

            LogHandshakeEnded(device.Id, EndReason.RequestExpired);
            LogInboundRequestExpired(device.Id, timeout.TotalSeconds);

            try
            {
                await request.RejectAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogInboundRequestExpiryRejectFailed(device.Id, ex);
            }

            ResetToVisible(device);

            if (_requestExpiries.TryRemove(device.Id, out var spent))
            {
                spent.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            // Answered, or the session stopped. Whoever cancelled owns the disposal.
        }
        catch (Exception ex)
        {
            LogInboundRequestExpiryFailed(device.Id, ex);
        }
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

    void CancelRefreshLoop() => _refreshCts?.Cancel();

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

                    // Discovery does not logically stop across a refresh, so the flag stays true
                    // and DiscoveryChanges publishes nothing. StartPump's SetFlag(true) below is
                    // then a no-op rather than the back half of a false/true blink.
                    await StopPumpAsync(_discover, clearFlag: false).ConfigureAwait(false);

                    var started = StartPump(_discover);

                    try
                    {
                        await started.Task.ConfigureAwait(false);
                    }
                    catch
                    {
                        await StopPumpAsync(_discover).ConfigureAwait(false);
                        throw;
                    }
                }
                finally
                {
                    _stateGate.Release();
                }

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

    async Task EvictAfterSettleAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(s_refreshSettleWindow, _timeProvider, cancellationToken).ConfigureAwait(false);

        _registry.EvictUnconfirmed();
    }

    static TaskCompletionSource StartPump(PumpState pump)
    {
        var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        pump.Cts = cts;
        pump.SetFlag(true);
        pump.Task = pump.Start(started, cts.Token);

        return started;
    }

    /// <param name="pump">The pump to stop.</param>
    /// <param name="clearFlag">
    /// Whether to report the operation as stopped. Pass <see langword="false"/> when the pump is
    /// being restarted immediately and the operation never logically stopped — a discovery refresh
    /// — so neither the flag nor its change stream reports a stop that did not happen.
    /// </param>
    static async Task StopPumpAsync(PumpState pump, bool clearFlag = true)
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

        if (clearFlag)
        {
            pump.SetFlag(false);
        }
    }

    sealed class PumpState(Func<TaskCompletionSource, CancellationToken, Task> start, Action<bool> setFlag)
    {
        public Func<TaskCompletionSource, CancellationToken, Task> Start { get; } = start;

        public Action<bool> SetFlag { get; } = setFlag;

        public CancellationTokenSource? Cts { get; set; }

        public Task? Task { get; set; }
    }
}