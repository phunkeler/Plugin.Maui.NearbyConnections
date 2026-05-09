using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tier-2 advertiser service that manages the advertising lifecycle, pending connection
/// requests, and active connections on the advertiser side.
/// </summary>
public partial class NearbyAdvertiser : INearbyAdvertiser
{
    readonly INearbyConnections _inner;
    readonly Action<Action> _dispatch;
    readonly ILogger _logger;
    CancellationTokenSource? _cts;
    readonly ObservableCollection<NearbyConnectionRequest> _pendingRequests = new();
    readonly ObservableCollection<NearbyConnection> _activeConnections = new();
    readonly Channel<(NearbyConnection, NearbyPayload)> _unifiedChannel =
        Channel.CreateUnbounded<(NearbyConnection, NearbyPayload)>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    /// <summary>
    /// Initializes a new <see cref="NearbyAdvertiser"/> with an explicit dispatch delegate.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyConnections"/> implementation.</param>
    /// <param name="dispatch">
    /// An action that marshals the given callback onto the required thread (typically the UI thread).
    /// All <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/> mutations are routed through this delegate.
    /// </param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyAdvertiser(INearbyConnections inner, Action<Action> dispatch, ILogger<NearbyAdvertiser>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(dispatch);
        _inner = inner;
        _dispatch = dispatch;
        _logger = logger ?? NullLogger<NearbyAdvertiser>.Instance;
    }

    /// <summary>
    /// Initializes a new <see cref="NearbyAdvertiser"/> using a MAUI <see cref="IDispatcher"/>
    /// to marshal collection mutations to the UI thread.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyConnections"/> implementation.</param>
    /// <param name="dispatcher">The MAUI dispatcher used for UI-thread marshalling.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyAdvertiser(INearbyConnections inner, IDispatcher dispatcher, ILogger<NearbyAdvertiser>? logger = null)
        : this(inner, action => dispatcher.Dispatch(action), logger)
    {
    }

    /// <inheritdoc/>
    public bool IsAdvertising { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<NearbyConnectionRequest> PendingRequests => _pendingRequests;

    /// <inheritdoc/>
    public IReadOnlyList<NearbyConnection> ActiveConnections => _activeConnections;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsAdvertising = true;
        LogAdvertisingStarted();
        _ = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        LogAdvertisingStopped();
        return Task.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var request in _inner.AdvertiseAsync(ct))
            {
                LogConnectionRequested(request.RemoteDevice.Id, request.RemoteDevice.DisplayName);
                OnConnectionRequested(request);
                _dispatch(() => _pendingRequests.Add(request));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsAdvertising = false;
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> AcceptAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var conn = await request.AcceptAsync(cancellationToken);
        _dispatch(() =>
        {
            _pendingRequests.Remove(request);
            _activeConnections.Add(conn);
        });
        LogConnectionAccepted(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        OnConnectionAccepted(conn);
        _ = MonitorConnectionAsync(conn);
        _ = ForwardPayloadsAsync(conn);
        return conn;
    }

    /// <inheritdoc/>
    public Task RejectAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        LogConnectionRejected(request.RemoteDevice.Id, request.RemoteDevice.DisplayName);
        _dispatch(() => _pendingRequests.Remove(request));
        return request.RejectAsync(cancellationToken);
    }

    private async Task MonitorConnectionAsync(NearbyConnection conn)
    {
        await conn.Disconnected;
        LogConnectionDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        OnConnectionDropped(conn);
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
    /// Called before a new inbound connection request is added to <see cref="PendingRequests"/>.
    /// Override in a subclass to react to incoming connection requests.
    /// </summary>
    /// <param name="request">The inbound connection request.</param>
    protected virtual void OnConnectionRequested(NearbyConnectionRequest request)
    {
    }

    /// <summary>
    /// Called after an accepted connection is added to <see cref="ActiveConnections"/>.
    /// Override in a subclass to react when a connection becomes active.
    /// </summary>
    /// <param name="connection">The newly accepted connection.</param>
    protected virtual void OnConnectionAccepted(NearbyConnection connection)
    {
    }

    /// <summary>
    /// Called after a disconnected connection is removed from <see cref="ActiveConnections"/>.
    /// Override in a subclass to react when a connection terminates.
    /// </summary>
    /// <param name="connection">The connection that was dropped.</param>
    protected virtual void OnConnectionDropped(NearbyConnection connection)
    {
    }
}
