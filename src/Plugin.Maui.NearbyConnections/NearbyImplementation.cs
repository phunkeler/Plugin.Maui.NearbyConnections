namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The default <see cref="INearby"/>: drives advertising and discovery through the platform
/// implementation and projects every platform callback into <see cref="Devices"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Threading contract.</strong> Platform callbacks arrive on SDK-owned background threads,
/// and this type does nothing to change that: <see cref="NearbyDeviceRegistry"/> is thread-safe,
/// connections are a concurrent dictionary, and both flags are volatile. Every member is callable
/// from any thread, and nothing here knows a UI thread exists. A consumer that binds to a user
/// interface marshals for itself, or uses <see cref="NearbyDeviceCollection"/>.
/// </para>
/// <para>
/// Start/stop state is guarded by <c>_stateGate</c> rather than an <see cref="Interlocked"/> flag:
/// the platform start calls are async, and a plain check-then-set let two concurrent
/// <c>StartAdvertisingAsync</c> calls both reach the platform.
/// </para>
/// </remarks>
sealed partial class NearbyImplementation : INearby, IAsyncDisposable
{
    // The interface, not the concrete implementation: on net10.0 every Platform* start throws, so a
    // concrete dependency would make the session untestable off-device. Tests substitute a fake.
    readonly IPlatformNearby _connections;
    readonly NearbyOptions _options;
    readonly ILogger _logger;

    readonly NearbyDeviceRegistry _registry = new();
    readonly SemaphoreSlim _stateGate = new(1, 1);

    /// <summary>
    /// Outstanding inbound requests, keyed by device id, so <see cref="AcceptAsync"/> and
    /// <see cref="RejectAsync"/> can find the request behind a device that reported
    /// <see cref="NearbyDeviceStatus.RequestReceived"/>. Entries are removed as soon as the request
    /// is answered.
    /// </summary>
    readonly ConcurrentDictionary<string, NearbyConnectionRequest> _pendingRequests
        = new(StringComparer.Ordinal);

    /// <summary>
    /// The live connections, keyed by device id. A dictionary rather than a field on
    /// <see cref="NearbyDevice"/>, which is an immutable snapshot and cannot carry a live handle.
    /// </summary>
    /// <remarks>
    /// <c>WatchDisconnectAsync</c> is the only place entries are removed, and that removal gates the
    /// device's return to <see cref="NearbyDeviceStatus.Visible"/>. Nothing else may clear this
    /// dictionary — see the comment in <see cref="StopAsync"/>.
    /// </remarks>
    readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections
        = new(StringComparer.Ordinal);

    /// <summary>
    /// The advertise and discover pumps, which differ only in the stream they drain and the flag
    /// they publish. Holding each one's cancellation source and task together keeps start and stop
    /// to a single implementation rather than two that must be kept in step.
    /// </summary>
    readonly PumpState _advertise;
    readonly PumpState _discover;

    // Volatile: written by whichever thread starts or stops a pump — including a platform callback
    // thread when a pump fails — and read by any thread at all.
    volatile bool _isAdvertising;
    volatile bool _isDiscovering;

    int _disposeGuard;

    internal NearbyImplementation(
        IPlatformNearby connections,
        NearbyOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _connections = connections;
        _options = options;
        _logger = logger;

        _advertise = new PumpState(
            start: ct => PumpAdvertiseAsync(_connections.AdvertiseAsync(ct), ct),
            setFlag: value => IsAdvertising = value);

        _discover = new PumpState(
            start: ct => PumpDiscoverAsync(_connections.DiscoverAsync(ct), ct),
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
                StartPump(_advertise);
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
                StartPump(_discover);
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
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsDiscovering)
            {
                await StopPumpAsync(_discover).ConfigureAwait(false);

                // Devices that were only ever visible are no longer meaningful once discovery
                // stops. Connected devices stay: stopping discovery does not end a conversation.
                _registry.RemoveWhere(static d => d.Status is NearbyDeviceStatus.Visible);
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
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
                    // One failed teardown must not abandon the rest.
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

        // Disposing the connection drives the platform disconnect. The drop is reported back
        // through the same path as a remote-initiated drop, so the device returns to Visible once,
        // from one place, regardless of which side ended it.
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

        // Before StopAsync: unsubscribing first means a backgrounding notification arriving during
        // teardown cannot start a second, concurrent StopAsync against a session already going away.
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
