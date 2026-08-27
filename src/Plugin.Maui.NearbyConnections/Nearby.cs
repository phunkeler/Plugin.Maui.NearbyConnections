namespace Plugin.Maui.NearbyConnections;

sealed partial class Nearby : INearby, IAsyncDisposable
{
    /// <summary>
    /// Bounds the teardown join on session-owned tasks. A constant rather than a
    /// <see cref="NearbyOptions"/> value: cancellation runs first, so the bound is the backstop,
    /// not the plan, and no consumer scenario wants a different value.
    /// </summary>
    static readonly TimeSpan s_sessionTaskJoinBound = TimeSpan.FromSeconds(5);

    readonly PumpState _advertise;
    readonly PumpState _discover;
    readonly CancellationTokenSource _disposing = new();
    readonly IPlatformNearby _connections;
    readonly NearbyOptions _options;
    readonly ILogger _logger;
    readonly TimeProvider _timeProvider;

    readonly DeviceRegistry _registry = new();
    readonly SemaphoreSlim _stateGate = new(1, 1);
    readonly RequestRegistry _requests;

    // Not volatile: the setters below take a ref to these, and ref-to-volatile is CS0420.
    // Interlocked.Exchange is a full fence, so every write is already published.
    bool _isAdvertising;
    bool _isDiscovering;

    readonly ChangeBroadcast<bool> _advertisingChanges = new();
    readonly ChangeBroadcast<bool> _discoveryChanges = new();
    readonly DiscoveryRefresher _refresher;
    readonly SessionTaskSet _tasks;
    readonly DeliveryBroadcast<NearbyConnectionRequest> _requestDeliveries;
    readonly DeliveryBroadcast<NearbyConnection> _connectionDeliveries;

    // The session stop token: StopAsync cancels it (DisposeAsync stops through StopAsync), so no
    // session-owned task survives a stop into the next session. Re-armed by the next start.
    CancellationTokenSource _stopCts = new();

    int _disposeGuard;

    internal Nearby(
        IPlatformNearby connections,
        NearbyOptions options,
        ILogger logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _connections = connections;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _requests = new RequestRegistry(_timeProvider, RunRequestExpiryAsync);
        _tasks = new SessionTaskSet(_timeProvider, onError: ex => LogSessionTaskFailed(ex));

        // The delivery streams read their replay sets from the facts' owners (C3's handover rule):
        // outstanding requests from the request registry, open connections from the platform table.
        _requestDeliveries = new DeliveryBroadcast<NearbyConnectionRequest>(() => _requests.Snapshot());
        _connectionDeliveries = new DeliveryBroadcast<NearbyConnection>(() => _connections.SnapshotConnections());
        _refresher = new DiscoveryRefresher(
            options.DiscoveryRefreshInterval,
            _timeProvider,
            _registry,
            RefreshDiscoveryOnceAsync,
            onFailed: ex => LogRefreshDiscoveryFailed(ex));

        _advertise = new PumpState(
            start: (started, ct) => PumpAdvertiseAsync(_connections.AdvertiseAsync(started, ct), started, ct),
            setFlag: value => IsAdvertising = value);

        _discover = new PumpState(
            start: (started, ct) => PumpDiscoverAsync(_connections.DiscoverAsync(started, ct), started, ct),
            setFlag: value => IsDiscovering = value);

        PlatformInitializeLifecycleObserver(logger);
    }

    /// <summary>
    /// Wires up backgrounding teardown on platforms that need it. A partial method rather than an
    /// inline <c>#if</c>, so the platform/shared boundary this codebase keeps checkable via file
    /// suffix extends to the session's own constructor and disposal, not just <c>Native/</c>.
    /// </summary>
    /// <remarks>
    /// On iOS this constructs the observer described on
    /// <c>Nearby.ios.cs</c>'s <c>_lifecycleObserver</c> field — no Android
    /// counterpart exists because the platform gives no equivalent backgrounding signal this
    /// session needs to react to.
    /// </remarks>
    partial void PlatformInitializeLifecycleObserver(ILogger logger);

    /// <summary>
    /// Unsubscribes the backgrounding observer and waits for a teardown it already started, if one
    /// was created. Compiles away on platforms with no observer.
    /// </summary>
    /// <remarks>
    /// Takes the teardown task by <see langword="ref"/> rather than returning it, because a
    /// <c>partial void</c> is what lets the call vanish on Android and <c>net10.0</c> — the
    /// platform-hook pattern this codebase uses instead of <c>#if</c>. A value-returning partial
    /// would need a stub file per platform for a member only iOS has.
    /// </remarks>
    partial void PlatformDisposeLifecycleObserver(ref ValueTask teardown);

    /// <inheritdoc/>
    public INearbyDevices Devices => _registry;

    /// <inheritdoc/>
    public bool IsAdvertising
    {
        get => _isAdvertising;

        // Publish only on a real transition. Every write site routes through this setter, so a
        // future one cannot forget to signal; Exchange also settles the one race that exists —
        // a faulting pump and StopPumpAsync both writing false — to a single publish.
        private set
        {
            if (Interlocked.Exchange(ref _isAdvertising, value) != value)
            {
                _advertisingChanges.Publish(value);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsDiscovering
    {
        get => _isDiscovering;

        private set
        {
            if (Interlocked.Exchange(ref _isDiscovering, value) != value)
            {
                _discoveryChanges.Publish(value);
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<NearbyConnectionRequest> Requests => _requestDeliveries.Stream;

    /// <inheritdoc/>
    public IAsyncEnumerable<NearbyConnection> Connections => _connectionDeliveries.Stream;

    /// <inheritdoc/>
    public IAsyncEnumerable<bool> AdvertisingChanges => _advertisingChanges.Stream;

    /// <inheritdoc/>
    public IAsyncEnumerable<bool> DiscoveryChanges => _discoveryChanges.Stream;

    /// <inheritdoc/>
    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => _connections.CheckAvailabilityAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!IsAdvertising)
            {
                EnsureStopTokenArmed();

                var started = StartPump(_advertise);

                try
                {
                    await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await StopPumpAsync(_advertise).ConfigureAwait(false);
                    throw;
                }
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsAdvertising)
            {
                await StopPumpAsync(_advertise).ConfigureAwait(false);
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!IsDiscovering)
            {
                EnsureStopTokenArmed();

                var started = StartPump(_discover);

                try
                {
                    await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await StopPumpAsync(_discover).ConfigureAwait(false);
                    throw;
                }

                _refresher.Start();
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        await _refresher.CancelAsync().ConfigureAwait(false);

        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsDiscovering)
            {
                await StopPumpAsync(_discover).ConfigureAwait(false);
                _registry.RemoveWhere(static d => d.Status is NearbyDeviceStatus.Visible);
            }
        }
        finally
        {
            _stateGate.Release();
        }

        await _refresher.DrainAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // The teardown order is the guarantee (section 4, decided item 4): cancel the session
        // stop token, stop the pumps under the gate, dispose connections, reject pending
        // requests, join the task set outside the gate, then clear the registry — so a joined
        // straggler's last transition lands before the clear instead of resurrecting a row.
        await _refresher.CancelAsync().ConfigureAwait(false);
        await _stopCts.CancelAsync().ConfigureAwait(false);

        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsAdvertising)
            {
                await StopPumpAsync(_advertise).ConfigureAwait(false);
            }

            if (IsDiscovering)
            {
                await StopPumpAsync(_discover).ConfigureAwait(false);
            }

            foreach (var connection in _connections.SnapshotConnections())
            {
                try
                {
                    connection.DisposeReason = NearbyEndReason.SessionStopped;
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogStopConnectionError(connection.RemoteDevice.Id, ex);
                }
            }

            foreach (var request in _requests.ClaimAll())
            {
                try
                {
                    // The stop already claimed the request, so the public path would refuse the
                    // reject; the session stopping is also what completes the request's Expired.
                    request.MarkExpired();
                    await request.RejectCore(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogStopRejectError(request.RemoteDevice.Id, ex);
                }
            }
        }
        finally
        {
            _stateGate.Release();
        }

        // Outside the gate: a joined task may need facade state. Cancellation already ran, so
        // the bound is the backstop, not the plan.
        if (!await _tasks.JoinAsync(s_sessionTaskJoinBound).ConfigureAwait(false))
        {
            LogSessionTaskJoinTimedOut(s_sessionTaskJoinBound.TotalSeconds);
        }

        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _registry.Clear(NearbyEndReason.SessionStopped);
        }
        finally
        {
            _stateGate.Release();
        }

        await _refresher.DrainAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        _registry.AddIfAbsent(device);
        Transition(device, NearbyDeviceStatus.Connecting, ConnectionRole.Initiator);

        try
        {
            var connection = await _connections.ConnectAsync(device, cancellationToken).ConfigureAwait(false);
            OnConnected(device, connection, ConnectionRole.Initiator);
            return connection;
        }
        catch (Exception ex)
        {
            var reason = ReasonFor(ex);
            LogHandshakeEnded(device.Id, reason);
            ResetToVisible(device, reason);
            throw;
        }
    }

    /// <summary>
    /// The session half of <see cref="NearbyConnectionRequest.AcceptAsync(CancellationToken)"/>,
    /// attached to each request its pump surfaces: the atomic claim, the registry transitions,
    /// and the delivery publish around the platform core.
    /// </summary>
    async Task<NearbyConnection> AcceptRequestAsync(NearbyConnectionRequest request, CancellationToken cancellationToken)
    {
        var device = request.RemoteDevice;

        if (!_requests.TryClaim(request))
        {
            throw new NearbyRequestExpiredException(
                $"The request from device '{device.Id}' is no longer outstanding — it expired or was already answered.");
        }

        Transition(device, NearbyDeviceStatus.Connecting, ConnectionRole.Acceptor);

        try
        {
            var connection = await request.AcceptCore(cancellationToken).ConfigureAwait(false);
            OnConnected(device, connection, ConnectionRole.Acceptor);
            return connection;
        }
        catch (Exception ex)
        {
            var reason = ReasonFor(ex);
            LogHandshakeEnded(device.Id, reason);
            ResetToVisible(device, reason);
            throw;
        }
    }

    /// <summary>The session half of <see cref="NearbyConnectionRequest.RejectAsync(CancellationToken)"/>.</summary>
    async Task RejectRequestAsync(NearbyConnectionRequest request, CancellationToken cancellationToken)
    {
        var device = request.RemoteDevice;

        if (!_requests.TryClaim(request))
        {
            throw new NearbyRequestExpiredException(
                $"The request from device '{device.Id}' is no longer outstanding — it expired or was already answered.");
        }

        await request.RejectCore(cancellationToken).ConfigureAwait(false);
        LogHandshakeEnded(device.Id, NearbyEndReason.RequestRejected);
        ResetToVisible(device, NearbyEndReason.RequestRejected);
    }

    /// <inheritdoc/>
    public bool TryGetConnection(string deviceId, [NotNullWhen(true)] out NearbyConnection? connection)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        // The platform's connection table is the one owner of this fact (C5); the session holds
        // no table of its own.
        return _connections.TryGetConnection(deviceId, out connection);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_connections.TryGetConnection(device.Id, out var connection))
        {
            return;
        }

        await connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Tears down the session: stops the session's own background work, disposes every active
    /// connection, then releases the platform session. Called by the DI container, not by app code
    /// — see the lifetime remarks on <see cref="INearby"/>.
    /// </summary>
    /// <remarks>
    /// Idempotent — a second call performs no additional work. Drain, then release: each step waits
    /// for the work that reads a handle before the next step frees it, so nothing here races a
    /// platform callback against a disposed object. A failure inside <see cref="StopAsync"/> is
    /// logged and does not stop teardown from completing.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        // First: releases the session's own work before anything it might touch is torn down.
        await _disposing.CancelAsync().ConfigureAwait(false);

        var lifecycleTeardown = ValueTask.CompletedTask;
        PlatformDisposeLifecycleObserver(ref lifecycleTeardown);
        await lifecycleTeardown.ConfigureAwait(false);

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogDisposeError(ex);
        }

        // Defensive second join: instant when StopAsync already emptied the set, and the only
        // join at all when StopAsync failed part-way (contract C6 — disposal joins too).
        if (!await _tasks.JoinAsync(s_sessionTaskJoinBound).ConfigureAwait(false))
        {
            LogSessionTaskJoinTimedOut(s_sessionTaskJoinBound.TotalSeconds);
        }

        await _connections.DisposeAsync().ConfigureAwait(false);
        _stateGate.Dispose();
        _stopCts.Dispose();
        _disposing.Dispose();
    }
}