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
    readonly List<Channel<AdvertiserEvent>> _subscribers = [];
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

    /// <inheritdoc/>
    public bool IsAdvertising { get; private set; }

    /// <inheritdoc/>
    public Task StartAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        // Expire any pending requests from a previous session so subscribers can clear their UI.
        lock (_stateLock)
        {
            foreach (var req in _pendingSnapshot)
            {
                Publish(new AdvertiserEvent.ConnectionRequestExpired(req));
            }
            _pendingSnapshot.Clear();
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

        // Expire pending requests — advertising stopped before they could be acted on.
        lock (_stateLock)
        {
            foreach (var req in _pendingSnapshot)
            {
                Publish(new AdvertiserEvent.ConnectionRequestExpired(req));
            }
            _pendingSnapshot.Clear();
        }

        LogAdvertisingStopped();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        lock (_stateLock)
        {
            foreach (var sub in _subscribers)
            {
                sub.Writer.TryComplete();
            }
            _subscribers.Clear();
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
        }

        foreach (var conn in connections)
        {
            await conn.DisposeAsync();
        }

        _cts?.CancelAsync();
        _cts?.Dispose();
        _cts = null;
        lock (_stateLock)
        {
            foreach (var sub in _subscribers)
            {
                sub.Writer.TryComplete();
            }
            _subscribers.Clear();
        }
        GC.SuppressFinalize(this);
    }

    // Must be called inside _stateLock.
    void Publish(AdvertiserEvent ev)
    {
        foreach (var sub in _subscribers)
        {
            sub.Writer.TryWrite(ev);
        }
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
                    Publish(new AdvertiserEvent.ConnectionRequested(request));
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
            IsAdvertising = false;
            if (fault is not null)
            {
                lock (_stateLock)
                {
                    foreach (var sub in _subscribers)
                    {
                        sub.Writer.TryComplete(fault);
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> AcceptAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var conn = await request.AcceptAsync(cancellationToken);
        conn.Role = ConnectionRole.Acceptor;
        var serviceToken = _cts?.Token ?? CancellationToken.None;
        lock (_stateLock)
        {
            _pendingSnapshot.Remove(request);
            _activeSnapshot.Add(conn);
            Publish(new AdvertiserEvent.ConnectionAccepted(conn));
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
                Publish(new AdvertiserEvent.ConnectionDropped(conn));
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
                    Publish(new AdvertiserEvent.PayloadReceived(conn, payload));
                }
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
        var sub = Channel.CreateUnbounded<AdvertiserEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        IEnumerable<AdvertiserEvent> snapshot;

        lock (_stateLock)
        {
            snapshot = _pendingSnapshot.Select(r => (AdvertiserEvent)new AdvertiserEvent.ConnectionRequested(r))
                .Concat(_activeSnapshot.Select(c => new AdvertiserEvent.ConnectionAccepted(c)))
                .ToList();
            _subscribers.Add(sub);
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
            lock (_stateLock) { _subscribers.Remove(sub); }
            sub.Writer.TryComplete();
        }
    }
}
