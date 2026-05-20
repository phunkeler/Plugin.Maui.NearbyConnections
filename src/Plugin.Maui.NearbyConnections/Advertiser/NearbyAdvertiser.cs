using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;


/// <summary>
/// Tier-2 advertiser service that manages the advertising lifecycle, pending connection
/// requests, and active connections on the advertiser side.
/// </summary>
public sealed partial class NearbyAdvertiser : INearbyAdvertiser
{
    readonly INearbyConnections _inner;
    readonly ILogger _logger;
    CancellationTokenSource? _cts;
    readonly ChannelBroadcaster<AdvertiserEvent> _broadcaster = new();
    readonly Lock _stateLock = new();
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

    volatile bool _isAdvertising;

    /// <inheritdoc/>
    public bool IsAdvertising => _isAdvertising;

    /// <inheritdoc/>
    public Task StartAsync()
    {
        CancellationTokenSource cts;
        lock (_stateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            foreach (var req in _pendingSnapshot)
            {
                _broadcaster.Publish(new AdvertiserEvent.ConnectionRequestExpired(req));
            }
            _pendingSnapshot.Clear();
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        _isAdvertising = true;
        LogAdvertisingStarted();
        _ = RunLoopAsync(cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        _isAdvertising = false;
        lock (_stateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            foreach (var req in _pendingSnapshot)
            {
                _broadcaster.Publish(new AdvertiserEvent.ConnectionRequestExpired(req));
            }
            _pendingSnapshot.Clear();
        }

        LogAdvertisingStopped();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_stateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _broadcaster.Complete();
        }
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        NearbyConnection[] connections;
        lock (_stateLock)
        {
            connections = [.. _activeSnapshot];
            _activeSnapshot.Clear();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        foreach (var conn in connections)
        {
            await conn.DisposeAsync();
        }

        lock (_stateLock)
        {
            _broadcaster.Complete();
        }
        GC.SuppressFinalize(this);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        Exception? fault = null;
        try
        {
            await foreach (var request in _inner.AdvertiseAsync(ct))
            {
                LogConnectionRequested(request.RemoteDevice.Id, request.RemoteDevice.DisplayName);
                lock (_stateLock)
                {
                    _pendingSnapshot.Add(request);
                    _broadcaster.Publish(new AdvertiserEvent.ConnectionRequested(request));
                }
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
            _isAdvertising = false;
            if (fault is not null)
            {
                lock (_stateLock)
                {
                    _broadcaster.Complete(fault);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> AcceptAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var conn = await request.AcceptAsync(cancellationToken);
        conn.Role = ConnectionRole.Acceptor;
        CancellationToken serviceToken;
        lock (_stateLock)
        {
            serviceToken = _cts?.Token ?? CancellationToken.None;
            _pendingSnapshot.Remove(request);
            _activeSnapshot.Add(conn);
            _broadcaster.Publish(new AdvertiserEvent.ConnectionAccepted(conn));
        }
        LogConnectionAccepted(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);

        _ = MonitorConnectionAsync(conn, serviceToken);
        _ = ForwardPayloadsAsync(conn, serviceToken);

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

    private async Task MonitorConnectionAsync(NearbyConnection conn, CancellationToken serviceToken)
    {
        try
        {
            await conn.Disconnected.WaitAsync(serviceToken);
            LogConnectionDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
            lock (_stateLock)
            {
                _activeSnapshot.Remove(conn);
                _broadcaster.Publish(new AdvertiserEvent.ConnectionDropped(conn));
            }
        }
        catch (OperationCanceledException)
        {
            lock (_stateLock) { _activeSnapshot.Remove(conn); }
        }
    }

    private async Task ForwardPayloadsAsync(NearbyConnection conn, CancellationToken ct)
    {
        try
        {
            await foreach (var payload in conn.ReceiveAsync(ct))
            {
                lock (_stateLock)
                {
                    _broadcaster.Publish(new AdvertiserEvent.PayloadReceived(conn, payload));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Service stopped; normal exit.
        }
        catch (Exception ex)
        {
            LogForwardPayloadsError(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName, ex);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AdvertiserEvent> EventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<AdvertiserEvent> sub;
        IEnumerable<AdvertiserEvent> snapshot;

        lock (_stateLock)
        {
            snapshot = _pendingSnapshot.Select(r => (AdvertiserEvent)new AdvertiserEvent.ConnectionRequested(r))
                .Concat(_activeSnapshot.Select(c => new AdvertiserEvent.ConnectionAccepted(c)))
                .ToList();
            sub = _broadcaster.Subscribe();
        }

        try
        {
            foreach (var ev in snapshot)
            {
                yield return ev;
            }

            yield return new AdvertiserEvent.Synchronized();

            await foreach (var ev in sub.Reader.ReadAllAsync(cancellationToken))
            {
                yield return ev;
            }
        }
        finally
        {
            lock (_stateLock) { _broadcaster.Unsubscribe(sub); }
        }
    }
}
