using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The default <see cref="INearbyConnections"/>: drives advertising and discovery through the platform
/// implementation, projects every platform callback into the observable <see cref="Devices"/>
/// collection, and raises lifecycle events on the UI dispatcher.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Threading contract.</strong> Platform callbacks arrive on SDK-owned background threads.
/// Every mutation of <see cref="Devices"/>, every <see cref="NearbyDevice"/> property write, and
/// every event raise is funnelled through <see cref="DispatchAsync"/>, so consumers observe all of
/// them on the UI thread. Nothing outside <c>Dispatch*</c> may touch device state.
/// </para>
/// <para>
/// Start/stop state is guarded by <c>_stateGate</c> rather than an <see cref="Interlocked"/> flag:
/// the platform start calls are async, and a plain check-then-set let two concurrent
/// <c>StartAdvertisingAsync</c> calls both reach the platform.
/// </para>
/// </remarks>
sealed partial class NearbyConnectionsImplementation : INearbyConnections, IAsyncDisposable
{
    // The interface, not the concrete implementation: on net10.0 every Platform* start throws, so a
    // concrete dependency would make the session untestable off-device. Tests substitute a fake.
    readonly IPlatformNearbyConnections _connections;
    readonly IDispatcher? _dispatcher;
    readonly ILogger _logger;

    readonly ObservableCollection<NearbyDevice> _devices = [];
    readonly SemaphoreSlim _stateGate = new(1, 1);

    /// <summary>
    /// Outstanding inbound requests, keyed by device id, so <see cref="AcceptAsync"/> and
    /// <see cref="RejectAsync"/> can find the request a <see cref="ConnectionRequested"/> handler
    /// was told about. Entries are removed as soon as the request is answered.
    /// </summary>
    readonly ConcurrentDictionary<string, NearbyConnectionRequest> _pendingRequests
        = new(StringComparer.Ordinal);

    /// <summary>
    /// The advertise and discover pumps, which differ only in the stream they drain and the flag
    /// they publish. Holding each one's cancellation source and task together keeps start and stop
    /// to a single implementation rather than two that must be kept in step.
    /// </summary>
    readonly PumpState _advertise;
    readonly PumpState _discover;

    int _disposeGuard;

#if IOS
    /// <summary>
    /// Tears the session down when iOS backgrounds the app. Owned by the session so it is
    /// unsubscribed on disposal — see <see cref="AppLifecycleObserver"/> for why this is required
    /// on iOS and has no Android counterpart.
    /// </summary>
    readonly AppLifecycleObserver _lifecycleObserver;
#endif

    internal NearbyConnectionsImplementation(
        IPlatformNearbyConnections connections,
        IDispatcher? dispatcher,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(logger);

        _connections = connections;
        _dispatcher = dispatcher;
        _logger = logger;

        Devices = new ReadOnlyObservableCollection<NearbyDevice>(_devices);

        _advertise = new PumpState(
            start: ct => PumpAdvertiseAsync(_connections.AdvertiseAsync(ct), ct),
            setFlag: value => DispatchAsync(() => IsAdvertising = value));

        _discover = new PumpState(
            start: ct => PumpDiscoverAsync(_connections.DiscoverAsync(ct), ct),
            setFlag: value => DispatchAsync(() => IsDiscovering = value));

#if IOS
        _lifecycleObserver = new AppLifecycleObserver(this, logger);
#endif
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A <see cref="ReadOnlyObservableCollection{T}"/>, so it implements
    /// <see cref="INotifyCollectionChanged"/> as the interface documents.
    /// </remarks>
    public IReadOnlyList<NearbyDevice> Devices { get; }

    /// <inheritdoc/>
    public bool IsAdvertising { get; private set; }

    /// <inheritdoc/>
    public bool IsDiscovering { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<NearbyConnectionRequestedEventArgs>? ConnectionRequested;

    /// <inheritdoc/>
    public event EventHandler<NearbyConnectionChangedEventArgs>? ConnectionEstablished;

    /// <inheritdoc/>
    public event EventHandler<NearbyConnectionChangedEventArgs>? ConnectionDropped;

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
                await StartPumpAsync(_advertise).ConfigureAwait(false);
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
    public async Task StartDiscoveringAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!IsDiscovering)
            {
                await StartPumpAsync(_discover).ConfigureAwait(false);
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopDiscoveringAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsDiscovering)
            {
                await StopPumpAsync(_discover).ConfigureAwait(false);

                // Devices that were only ever visible are no longer meaningful once discovery
                // stops. Connected devices stay: stopping discovery does not end a conversation.
                await RemoveVisibleDevicesAsync().ConfigureAwait(false);
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

            // Snapshot: disconnecting mutates the collection from the dispatcher.
            var connected = _devices
                .Select(d => d.State)
                .OfType<DeviceState.Connected>()
                .Select(s => s.Connection)
                .ToArray();

            foreach (var connection in connected)
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

            await DispatchAsync(_devices.Clear).ConfigureAwait(false);
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

        await TransitionAsync(device, new DeviceState.Connecting(ConnectionRole.Initiator)).ConfigureAwait(false);

        try
        {
            var connection = await _connections.ConnectAsync(device, cancellationToken).ConfigureAwait(false);
            await OnConnectedAsync(device, connection, ConnectionRole.Initiator).ConfigureAwait(false);
            return connection;
        }
        catch (Exception ex)
        {
            // The handshake failed or was cancelled: the device is still out there, just not
            // connected. Anything other than resetting leaves a row stuck on "Connecting" forever.
            var reason = ReasonFor(ex);
            LogHandshakeEnded(device.Id, reason);
            await ResetToVisibleAsync(device).ConfigureAwait(false);
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

        await TransitionAsync(device, new DeviceState.Connecting(ConnectionRole.Acceptor)).ConfigureAwait(false);

        try
        {
            var connection = await request.AcceptAsync(cancellationToken).ConfigureAwait(false);
            await OnConnectedAsync(device, connection, ConnectionRole.Acceptor).ConfigureAwait(false);
            return connection;
        }
        catch (Exception ex)
        {
            var reason = ReasonFor(ex);
            LogHandshakeEnded(device.Id, reason);
            await ResetToVisibleAsync(device).ConfigureAwait(false);
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

        await ResetToVisibleAsync(device).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device.State is not DeviceState.Connected { Connection: var connection })
        {
            return;
        }

        // Disposing the connection drives the platform disconnect. The drop is reported back
        // through the same path as a remote-initiated drop, so ConnectionDropped is raised once,
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

#if IOS
        // Before StopAsync: unsubscribing first means a backgrounding notification arriving during
        // teardown cannot start a second, concurrent StopAsync against a session already going away.
        _lifecycleObserver.Dispose();
#endif

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
