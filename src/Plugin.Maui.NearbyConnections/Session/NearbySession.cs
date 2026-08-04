using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The default <see cref="INearbySession"/>: drives advertising and discovery through the platform
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
sealed partial class NearbySession : INearbySession, IAsyncDisposable
{
    readonly NearbyConnectionsImplementation _connections;
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

    CancellationTokenSource? _advertiseCts;
    CancellationTokenSource? _discoverCts;
    Task? _advertisePump;
    Task? _discoverPump;

    int _disposeGuard;

    internal NearbySession(
        NearbyConnectionsImplementation connections,
        IDispatcher? dispatcher,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(logger);

        _connections = connections;
        _dispatcher = dispatcher;
        _logger = logger;

        Devices = new ReadOnlyObservableCollection<NearbyDevice>(_devices);
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
    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsAdvertising)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            var stream = _connections.AdvertiseAsync(cts.Token);

            // The platform start is inside the enumerable, so failures surface at the first MoveNext
            // rather than here. Pump on a background task and let the first move happen there; the
            // stream faulting is reported through the pump's catch, not from this method.
            _advertiseCts = cts;
            _advertisePump = PumpAdvertiseAsync(stream, cts.Token);

            await SetIsAdvertisingAsync(true).ConfigureAwait(false);
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
            if (!IsAdvertising)
            {
                return;
            }

            await StopAdvertisingCoreAsync().ConfigureAwait(false);
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
            if (IsDiscovering)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            var stream = _connections.DiscoverAsync(cts.Token);

            _discoverCts = cts;
            _discoverPump = PumpDiscoverAsync(stream, cts.Token);

            await SetIsDiscoveringAsync(true).ConfigureAwait(false);
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
            if (!IsDiscovering)
            {
                return;
            }

            await StopDiscoveringCoreAsync().ConfigureAwait(false);
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
                await StopAdvertisingCoreAsync().ConfigureAwait(false);
            }

            if (IsDiscovering)
            {
                await StopDiscoveringCoreAsync().ConfigureAwait(false);
            }

            // Snapshot: disconnecting mutates the collection from the dispatcher.
            var connected = _devices
                .Where(d => d.Connection is not null)
                .Select(d => d.Connection!)
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

        await DispatchAsync(() =>
        {
            device.Role = ConnectionRole.Initiator;
            device.Status = NearbyDeviceStatus.Connecting;
        }).ConfigureAwait(false);

        try
        {
            var connection = await _connections.ConnectAsync(device, cancellationToken).ConfigureAwait(false);
            await OnConnectedAsync(device, connection, ConnectionRole.Initiator).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            // The handshake failed or was cancelled: the device is still out there, just not
            // connected. Anything other than resetting leaves a row stuck on "Connecting" forever.
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

        await DispatchAsync(() =>
        {
            device.Role = ConnectionRole.Acceptor;
            device.Status = NearbyDeviceStatus.Connecting;
        }).ConfigureAwait(false);

        try
        {
            var connection = await request.AcceptAsync(cancellationToken).ConfigureAwait(false);
            await OnConnectedAsync(device, connection, ConnectionRole.Acceptor).ConfigureAwait(false);
            return connection;
        }
        catch
        {
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
        await ResetToVisibleAsync(device).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device.Connection is not { } connection)
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
