using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tier-2 discoverer service that manages the discovery lifecycle, nearby visible devices,
/// and active connections on the discoverer side.
/// </summary>
public partial class NearbyDiscoverer : INearbyDiscoverer
{
    readonly INearbyConnections _inner;
    readonly Action<Action> _dispatch;
    readonly ILogger _logger;
    CancellationTokenSource? _cts;
    readonly ObservableCollection<NearbyDevice> _nearbyDevices = new();
    readonly ObservableCollection<NearbyConnection> _activeConnections = new();
    readonly Channel<(NearbyConnection, NearbyPayload)> _unifiedChannel =
        Channel.CreateUnbounded<(NearbyConnection, NearbyPayload)>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    /// <summary>
    /// Initializes a new <see cref="NearbyDiscoverer"/> with an explicit dispatch delegate.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyConnections"/> implementation.</param>
    /// <param name="dispatch">
    /// An action that marshals the given callback onto the required thread (typically the UI thread).
    /// All <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/> mutations are routed through this delegate.
    /// </param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyDiscoverer(INearbyConnections inner, Action<Action> dispatch, ILogger<NearbyDiscoverer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(dispatch);
        _inner = inner;
        _dispatch = dispatch;
        _logger = logger ?? NullLogger<NearbyDiscoverer>.Instance;
    }

    /// <summary>
    /// Initializes a new <see cref="NearbyDiscoverer"/> using a MAUI <see cref="IDispatcher"/>
    /// to marshal collection mutations to the UI thread.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyConnections"/> implementation.</param>
    /// <param name="dispatcher">The MAUI dispatcher used for UI-thread marshalling.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyDiscoverer(INearbyConnections inner, IDispatcher dispatcher, ILogger<NearbyDiscoverer>? logger = null)
        : this(inner, action => dispatcher.Dispatch(action), logger)
    {
    }

    /// <inheritdoc/>
    public bool IsDiscovering { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<NearbyDevice> NearbyDevices => _nearbyDevices;

    /// <inheritdoc/>
    public IReadOnlyList<NearbyConnection> ActiveConnections => _activeConnections;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsDiscovering = true;
        LogDiscoveryStarted();
        _ = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        LogDiscoveryStopped();
        return Task.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var ev in _inner.DiscoverAsync(ct))
            {
                if (ev.Type == NearbyDeviceEventType.Found)
                {
                    LogDeviceFound(ev.Device.Id, ev.Device.DisplayName);
                    OnDeviceFound(ev.Device);
                    _dispatch(() => _nearbyDevices.Add(ev.Device));
                }
                else
                {
                    LogDeviceLost(ev.Device.Id, ev.Device.DisplayName);
                    OnDeviceLost(ev.Device);
                    _dispatch(() => _nearbyDevices.Remove(ev.Device));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        LogConnecting(device.Id, device.DisplayName);
        _dispatch(() => _nearbyDevices.Remove(device));
        var conn = await _inner.ConnectAsync(device, cancellationToken);
        _dispatch(() => _activeConnections.Add(conn));
        LogConnected(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        OnConnected(conn);
        _ = MonitorConnectionAsync(conn);
        _ = ForwardPayloadsAsync(conn);
        return conn;
    }

    private async Task MonitorConnectionAsync(NearbyConnection conn)
    {
        await conn.Disconnected;
        LogConnectionDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        OnDisconnected(conn);
        _dispatch(() => _activeConnections.Remove(conn));
    }

    private async Task ForwardPayloadsAsync(NearbyConnection conn)
    {
        try
        {
            await foreach (var payload in conn.ReceiveAsync())
            {
                _unifiedChannel.Writer.TryWrite((conn, payload));
            }
        }
        catch (Exception)
        {
            // Exceptions indicate the connection dropped; MonitorConnectionAsync handles cleanup.
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<(NearbyConnection Connection, NearbyPayload Payload)> ReceiveAllAsync(CancellationToken cancellationToken = default)
        => _unifiedChannel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Called before a newly found device is added to <see cref="NearbyDevices"/>.
    /// Override in a subclass to react when a nearby device is discovered.
    /// </summary>
    /// <param name="device">The device that was found.</param>
    protected virtual void OnDeviceFound(NearbyDevice device)
    {
    }

    /// <summary>
    /// Called before a lost device is removed from <see cref="NearbyDevices"/>.
    /// Override in a subclass to react when a nearby device disappears.
    /// </summary>
    /// <param name="device">The device that was lost.</param>
    protected virtual void OnDeviceLost(NearbyDevice device)
    {
    }

    /// <summary>
    /// Called after a connection is added to <see cref="ActiveConnections"/>.
    /// Override in a subclass to react when a connection becomes active.
    /// </summary>
    /// <param name="connection">The newly established connection.</param>
    protected virtual void OnConnected(NearbyConnection connection)
    {
    }

    /// <summary>
    /// Called after a disconnected connection is removed from <see cref="ActiveConnections"/>.
    /// Override in a subclass to react when a connection terminates.
    /// </summary>
    /// <param name="connection">The connection that was dropped.</param>
    protected virtual void OnDisconnected(NearbyConnection connection)
    {
    }
}
