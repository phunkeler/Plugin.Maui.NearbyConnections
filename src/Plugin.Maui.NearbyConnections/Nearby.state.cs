namespace Plugin.Maui.NearbyConnections;

sealed partial class Nearby
{
    static NearbyEndReason ReasonFor(Exception exception) => exception switch
    {
        OperationCanceledException => NearbyEndReason.Cancelled,
        NearbyConnectionTimeoutException => NearbyEndReason.TimedOut,
        _ => NearbyEndReason.Failed,
    };

    void Transition(
        NearbyDevice device,
        NearbyDeviceStatus status,
        ConnectionRole? role,
        DateTimeOffset? requestExpiresAt = null,
        NearbyEndReason? reason = null)
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
                },
            reason);

    async Task PumpAdvertiseAsync(
        IAsyncEnumerable<NearbyConnectionRequest> stream,
        TaskCompletionSource started,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                OnRequestReceived(request);
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

        if (_connections.TryGetConnection(device.Id, out _)
            || _requests.Contains(device.Id))
        {
            return;
        }

        if (_registry.TryGet(device.Id, out var known)
            && known.Status is NearbyDeviceStatus.Visible)
        {
            _registry.Remove(device.Id, NearbyEndReason.LostFromDiscovery);
        }
    }

    void OnRequestReceived(NearbyConnectionRequest request)
    {
        var device = request.RemoteDevice;

        if (_options.AutoAcceptConnectionRequests)
        {
            _tasks.Add(AutoAcceptAsync(request, device, _stopCts.Token));
            return;
        }

        // Attach the session half before the request becomes claimable or visible: the public
        // answer operations must run the claim and the registry effects from their first caller.
        request.AttachSession(
            ct => AcceptRequestAsync(request, ct),
            ct => RejectRequestAsync(request, ct));

        var expiresAt = _requests.Track(request);
        _registry.AddIfAbsent(device);

        Transition(
            device,
            NearbyDeviceStatus.RequestReceived,
            role: null,
            expiresAt);

        _requestDeliveries.Publish(request);
    }

    /// <summary>
    /// The expiry effects <see cref="RequestRegistry"/> runs for a request whose timer won the
    /// claim: reject, reset the device row, and log — all session-side, so device-state mutation
    /// keeps one path. Never throws.
    /// </summary>
    async Task RunRequestExpiryAsync(NearbyConnectionRequest request)
    {
        var device = request.RemoteDevice;

        try
        {
            request.MarkExpired();
            LogHandshakeEnded(device.Id, NearbyEndReason.RequestExpired);
            LogInboundRequestExpired(device.Id, _options.InboundRequestTimeout.TotalSeconds);

            try
            {
                // The core, not the public operation: the timer already won the claim, so the
                // public path would refuse this reject as no longer outstanding.
                await request.RejectCore(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogInboundRequestExpiryRejectFailed(device.Id, ex);
            }

            ResetToVisible(device, NearbyEndReason.RequestExpired);
        }
        catch (Exception ex)
        {
            LogInboundRequestExpiryFailed(device.Id, ex);
        }
    }

    async Task AutoAcceptAsync(NearbyConnectionRequest request, NearbyDevice device, CancellationToken stopToken)
    {
        _registry.AddIfAbsent(device);
        Transition(device, NearbyDeviceStatus.Connecting, ConnectionRole.Acceptor);

        try
        {
            // The platform core directly: auto-accept never tracks the request, so there is no
            // claim to run and INearby.Requests never yields it.
            var connection = await request.AcceptCore(stopToken).ConfigureAwait(false);
            OnConnected(device, connection, ConnectionRole.Acceptor);
        }
        catch (Exception ex)
        {
            var reason = ReasonFor(ex);
            LogHandshakeEnded(device.Id, reason);
            LogAutoAcceptFailed(device.Id, ex);
            ResetToVisible(device, reason);
        }
    }

    void OnConnected(NearbyDevice device, NearbyConnection connection, ConnectionRole role)
    {
        _registry.AddIfAbsent(device);
        Transition(device, NearbyDeviceStatus.Connected, role);
        _tasks.Add(WatchDisconnectAsync(device, connection));
        _connectionDeliveries.Publish(connection);
    }

    async Task WatchDisconnectAsync(NearbyDevice device, NearbyConnection connection)
    {
        try
        {
            var reason = await connection.Disconnected.ConfigureAwait(false);

            // The platform's table clears itself on release; this watcher keeps only the registry
            // transition. When the platform already holds a NEWER connection for the device, that
            // connection's own watcher owns the row — resetting here would clobber it.
            if (_connections.TryGetConnection(device.Id, out var current)
                && !ReferenceEquals(current, connection))
            {
                return;
            }

            // No disposal guard needed: ResetToVisible reaches the registry through Update, which
            // returns early for an id it does not hold. Disposal clears the registry first, so a
            // watcher waking afterwards finds nothing to update and cannot resurrect a row.
            ResetToVisible(device, reason);
        }
        catch (Exception ex)
        {
            LogDisconnectWatchFailed(device.Id, ex);
        }
    }

    void ResetToVisible(NearbyDevice device, NearbyEndReason? reason = null)
        => Transition(device, NearbyDeviceStatus.Visible, role: null, requestExpiresAt: null, reason);

    /// <summary>
    /// Re-arms the session stop token after a stop. Runs under the state gate in the start
    /// operations, so a new session's tasks never observe the previous session's cancellation.
    /// </summary>
    void EnsureStopTokenArmed()
    {
        if (!_stopCts.IsCancellationRequested)
        {
            return;
        }

        var spent = _stopCts;
        _stopCts = new CancellationTokenSource();
        spent.Dispose();
    }

    /// <summary>
    /// One gated discovery restart, run by <see cref="DiscoveryRefresher"/> each interval. Returns
    /// <see langword="false"/> when discovery stopped, which ends the refresh loop. The state gate
    /// never leaves the facade — the refresher only calls this delegate.
    /// </summary>
    async Task<bool> RefreshDiscoveryOnceAsync(CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!IsDiscovering)
            {
                return false;
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

            return true;
        }
        finally
        {
            _stateGate.Release();
        }
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