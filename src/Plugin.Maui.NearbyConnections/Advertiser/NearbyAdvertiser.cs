using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tier-2 advertiser service that manages the advertising lifecycle, pending connection
/// requests, and active connections on the advertiser side.
/// </summary>
public partial class NearbyAdvertiser : INearbyAdvertiser, IDisposable
{
    readonly INearbyConnections _inner;
    readonly ILogger _logger;
    CancellationTokenSource? _cts;
    Channel<AdvertiserEvent> _eventChannel = Channel.CreateUnbounded<AdvertiserEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    readonly object _stateLock = new();
    readonly List<NearbyConnectionRequest> _pendingSnapshot = [];
    readonly List<NearbyConnection> _activeSnapshot = [];

    /// <summary>
    /// Initializes a new <see cref="NearbyAdvertiser"/>.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyConnections"/> implementation.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyAdvertiser(INearbyConnections inner, ILogger<NearbyAdvertiser>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _logger = logger ?? NullLogger<NearbyAdvertiser>.Instance;
    }

    /// <inheritdoc/>
    public bool IsAdvertising { get; private set; }

    /// <inheritdoc/>
    public Task StartAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _eventChannel.Writer.TryComplete();
        _eventChannel = Channel.CreateUnbounded<AdvertiserEvent>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        lock (_stateLock)
        {
            _pendingSnapshot.Clear();
            _activeSnapshot.Clear();
        }
        _cts = new CancellationTokenSource();
        IsAdvertising = true;
        LogAdvertisingStarted();
        _ = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        IsAdvertising = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _eventChannel.Writer.TryComplete();
        LogAdvertisingStopped();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _eventChannel.Writer.TryComplete();
        GC.SuppressFinalize(this);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var eventChannel = _eventChannel;
        Exception? fault = null;
        try
        {
            await foreach (var request in _inner.AdvertiseAsync(ct))
            {
                LogConnectionRequested(request.RemoteDevice.Id, request.RemoteDevice.DisplayName);
                lock (_stateLock)
                {
                    _pendingSnapshot.Add(request);
                }
                eventChannel.Writer.TryWrite(new AdvertiserEvent.ConnectionRequested(request));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit when StopAsync() cancels the token.
        }
        catch (Exception ex)
        {
            fault = ex;
        }
        finally
        {
            IsAdvertising = false;
            eventChannel.Writer.TryComplete(fault);
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> AcceptAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var conn = await request.AcceptAsync(cancellationToken);
        conn.Role = ConnectionRole.Acceptor;
        lock (_stateLock)
        {
            _pendingSnapshot.Remove(request);
            _activeSnapshot.Add(conn);
        }
        LogConnectionAccepted(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        var serviceToken = _cts?.Token ?? CancellationToken.None;
        var channel = _eventChannel;
        channel.Writer.TryWrite(new AdvertiserEvent.ConnectionAccepted(conn));
        _ = MonitorConnectionAsync(conn, channel, serviceToken);
        _ = ForwardPayloadsAsync(conn, channel, serviceToken);
        return conn;
    }

    /// <inheritdoc/>
    public Task RejectAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        LogConnectionRejected(request.RemoteDevice.Id, request.RemoteDevice.DisplayName);
        lock (_stateLock)
        {
            _pendingSnapshot.Remove(request);
        }
        return request.RejectAsync(cancellationToken);
    }

    private async Task MonitorConnectionAsync(NearbyConnection conn, Channel<AdvertiserEvent> channel, CancellationToken serviceToken)
    {
        try
        {
            await conn.Disconnected.WaitAsync(serviceToken);
            LogConnectionDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
            lock (_stateLock) { _activeSnapshot.Remove(conn); }
            channel.Writer.TryWrite(new AdvertiserEvent.ConnectionDropped(conn));
        }
        catch (OperationCanceledException)
        {
            lock (_stateLock) { _activeSnapshot.Remove(conn); }
        }
    }

    private static async Task ForwardPayloadsAsync(NearbyConnection conn, Channel<AdvertiserEvent> channel, CancellationToken ct)
    {
        try
        {
            await foreach (var payload in conn.ReceiveAsync(ct))
            {
                channel.Writer.TryWrite(new AdvertiserEvent.PayloadReceived(conn, payload));
            }
        }
        catch (OperationCanceledException)
        {
            // Service stopped; normal exit.
        }
        catch (Exception)
        {
            // Connection dropped; MonitorConnectionAsync handles cleanup.
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AdvertiserEvent> EventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IEnumerable<AdvertiserEvent> snapshot;
        lock (_stateLock)
        {
            snapshot = _pendingSnapshot.Select(r => (AdvertiserEvent)new AdvertiserEvent.ConnectionRequested(r))
                .Concat(_activeSnapshot.Select(c => new AdvertiserEvent.ConnectionAccepted(c)))
                .ToList();
        }

        foreach (var ev in snapshot)
        {
            yield return ev;
        }

        yield return new AdvertiserEvent.Synchronized();

        await foreach (var ev in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return ev;
        }
    }
}
