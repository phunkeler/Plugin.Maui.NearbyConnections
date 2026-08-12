namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The default implementation of <see cref="INearby"/>. Drives advertising and discovery through
/// the injected <see cref="IPlatformNearby"/> and projects every platform callback into
/// <see cref="Devices"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Threading contract.</strong> Platform callbacks arrive on SDK-owned background threads,
/// and this type does nothing to marshal off of them: <see cref="NearbyDeviceRegistry"/> is
/// thread-safe by construction, the connection and pending-request dictionaries are
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> instances, and the advertising/discovering flags
/// are <see langword="volatile"/>. Every member is callable from any thread, and nothing here
/// assumes a UI thread exists — a consumer that binds device state to a user interface marshals for
/// itself, or constructs a <see cref="NearbyDeviceCollection"/>.
/// </para>
/// <para>
/// Start/stop state is guarded by a <see cref="SemaphoreSlim"/> rather than an
/// <see cref="Interlocked"/> flag, because the platform start calls are asynchronous: a plain
/// check-then-set would let two concurrent <see cref="StartAdvertisingAsync"/> calls both reach the
/// platform before either observed the other's flag.
/// </para>
/// </remarks>
sealed partial class NearbyImplementation : INearby, IAsyncDisposable
{
    readonly IPlatformNearby _connections;
    readonly NearbyOptions _options;
    readonly ILogger _logger;
    readonly TimeProvider _timeProvider;

    readonly NearbyDeviceRegistry _registry = new();
    readonly SemaphoreSlim _stateGate = new(1, 1);

    readonly ConcurrentDictionary<string, NearbyConnectionRequest> _pendingRequests
        = new(StringComparer.Ordinal);

    readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections
        = new(StringComparer.Ordinal);

    readonly PumpState _advertise;
    readonly PumpState _discover;

    volatile bool _isAdvertising;
    volatile bool _isDiscovering;

    /// <summary>
    /// How long a restarted discovery pass is given to re-report devices before the ones it did not
    /// mention are evicted. Two seconds is comfortably longer than either platform takes to re-fire
    /// its found callback for a device already in range.
    /// </summary>
    static readonly TimeSpan RefreshSettleWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The discovery refresh loop, live only while discovering and only when the options ask for
    /// one. Guarded by <c>_stateGate</c> like the pumps.
    /// </summary>
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
        private set => _isAdvertising = value;
    }

    /// <inheritdoc/>
    public bool IsDiscovering
    {
        get => _isDiscovering;
        private set => _isDiscovering = value;
    }

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
                    // Clean up the failed pump so a retry starts fresh rather than reusing a dead
                    // task/cts pair.
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
        // Before the gate: the refresh loop takes it, so it must be released from its wait before
        // this call can acquire it. Draining happens after the gate is released again.
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
        // Before the gate, for the same reason as StopDiscoveryAsync.
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

            // Snapshot: disposing removes entries from the dictionary as each drop is watched.
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

            // Reject anything still outstanding so remote devices are not left hanging.
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

            // _activeConnections is deliberately NOT cleared here. Each entry is removed by the
            // WatchDisconnectAsync continuation that observes its own connection dropping, and that
            // removal is what gates the device's return to Visible. Clearing eagerly makes every
            // one of those removals fail its identity check, and the drop is never recorded.

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

        // Surfaced first: connecting to a device the session has not seen (an id kept across a
        // discovery restart, say) must still produce a device that changes are reported for.
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
            // The handshake failed or was cancelled: the device is still out there, just not
            // connected. Anything other than resetting leaves a row stuck on "Connecting" forever.
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
                $"No connection request is outstanding for device '{device.Id}'. A request can only be accepted once, and only before it expires.");
        }

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

    /// <summary>
    /// Tears down the session. Internal by design: the container owns this singleton, and a public
    /// <c>DisposeAsync</c> would invite <c>await using</c> in a page, killing the session app-wide.
    /// Consumers use <see cref="StopAsync"/>.
    /// </summary>
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
            // Teardown must not throw out of container disposal.
            LogDisposeError(ex);
        }

        await _connections.DisposeAsync().ConfigureAwait(false);
        _stateGate.Dispose();
    }
}
