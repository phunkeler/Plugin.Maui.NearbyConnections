namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyImplementation : INearby, IAsyncDisposable
{
    static readonly TimeSpan s_refreshSettleWindow = TimeSpan.FromSeconds(2);

    readonly PumpState _advertise;
    readonly PumpState _discover;
    readonly IPlatformNearby _connections;
    readonly NearbyOptions _options;
    readonly ILogger _logger;
    readonly TimeProvider _timeProvider;

    readonly NearbyDeviceRegistry _registry = new();
    readonly SemaphoreSlim _stateGate = new(1, 1);
    readonly ConcurrentDictionary<string, NearbyConnectionRequest> _pendingRequests
        = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, CancellationTokenSource> _requestExpiries
        = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections
        = new(StringComparer.Ordinal);

    // Not volatile: the setters below take a ref to these, and ref-to-volatile is CS0420.
    // Interlocked.Exchange is a full fence, so every write is already published.
    bool _isAdvertising;
    bool _isDiscovering;

    readonly ChangeBroadcast<bool> _advertisingChanges = new();
    readonly ChangeBroadcast<bool> _discoveryChanges = new();

    CancellationTokenSource? _refreshCts;
    Task? _refreshTask;
    int _disposeGuard;

    internal NearbyImplementation(
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
    /// <c>NearbyImplementation.ios.cs</c>'s <c>_lifecycleObserver</c> field — no Android
    /// counterpart exists because the platform gives no equivalent backgrounding signal this
    /// session needs to react to.
    /// </remarks>
    partial void PlatformInitializeLifecycleObserver(ILogger logger);

    /// <summary>
    /// Unsubscribes the backgrounding observer, if one was created. No-op where none exists.
    /// </summary>
    partial void PlatformDisposeLifecycleObserver();

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

                StartRefreshLoop();
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
        CancelRefreshLoop();

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

        await DrainRefreshLoopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancelRefreshLoop();

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

            foreach (var (_, connection) in _activeConnections.ToArray())
            {
                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogStopConnectionError(connection.RemoteDevice.Id, ex);
                }
            }

            foreach (var (deviceId, _) in _requestExpiries.ToArray())
            {
                DisarmRequestExpiry(deviceId);
            }

            foreach (var (_, request) in _pendingRequests.ToArray())
            {
                try
                {
                    await request.RejectAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogStopRejectError(request.RemoteDevice.Id, ex);
                }
            }

            _pendingRequests.Clear();
            _registry.Clear();
        }
        finally
        {
            _stateGate.Release();
        }

        await DrainRefreshLoopAsync().ConfigureAwait(false);
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
            ResetToVisible(device);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> AcceptAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_pendingRequests.TryRemove(device.Id, out var request))
        {
            throw new InvalidOperationException(
                $"No connection request is outstanding for device '{device.Id}'. " +
                $"A request can only be accepted once, and only before it expires.");
        }

        DisarmRequestExpiry(device.Id);
        Transition(device, NearbyDeviceStatus.Connecting, ConnectionRole.Acceptor);

        try
        {
            var connection = await request.AcceptAsync(cancellationToken).ConfigureAwait(false);
            OnConnected(device, connection, ConnectionRole.Acceptor);
            return connection;
        }
        catch (Exception ex)
        {
            var reason = ReasonFor(ex);
            LogHandshakeEnded(device.Id, reason);
            ResetToVisible(device);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RejectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_pendingRequests.TryRemove(device.Id, out var request))
        {
            throw new InvalidOperationException(
                $"No connection request is outstanding for device '{device.Id}'. A request can only be rejected once, and only before it expires.");
        }

        DisarmRequestExpiry(device.Id);
        await request.RejectAsync(cancellationToken).ConfigureAwait(false);
        LogHandshakeEnded(device.Id, EndReason.LocalRejected);
        ResetToVisible(device);
    }

    /// <inheritdoc/>
    public bool TryGetConnection(string deviceId, [NotNullWhen(true)] out NearbyConnection? connection)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        return _activeConnections.TryGetValue(deviceId, out connection);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_activeConnections.TryGetValue(device.Id, out var connection))
        {
            return;
        }

        await connection.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        PlatformDisposeLifecycleObserver();

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogDisposeError(ex);
        }

        await _connections.DisposeAsync().ConfigureAwait(false);
        _stateGate.Dispose();
    }
}