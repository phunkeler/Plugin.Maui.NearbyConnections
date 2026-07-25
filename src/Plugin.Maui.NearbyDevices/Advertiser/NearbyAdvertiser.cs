using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace Plugin.Maui.NearbyDevices;


/// <summary>
/// Tier-2 advertiser service that manages the advertising lifecycle, pending connection
/// requests, and active connections on the advertiser side.
/// </summary>
public sealed partial class NearbyAdvertiser : INearbyAdvertiser
{
    readonly INearbyDevices _inner;
    readonly ILogger _logger;
    readonly ConnectionLifecycle<NearbyConnectionRequest, AdvertiserEvent> _lifecycle = new();

    /// <summary>
    /// Initializes a new <see cref="NearbyAdvertiser"/>.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyDevices"/> implementation.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyAdvertiser(INearbyDevices inner, ILogger<NearbyAdvertiser>? logger = null)
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
        return _lifecycle.StartAsync(
            executeTaskFactory: RunLoopAsync,
            onPendingExpired: req => new AdvertiserEvent.ConnectionRequestExpired(req),
            setRunningFlag: v =>
            {
                _isAdvertising = v;
                if (v)
                {
                    LogAdvertisingStarted();
                }
            });
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        return _lifecycle.StopAsync(
            onPendingExpired: req => new AdvertiserEvent.ConnectionRequestExpired(req),
            setRunningFlag: v =>
            {
                _isAdvertising = v;
                if (!v)
                {
                    LogAdvertisingStopped();
                }
            });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lifecycle.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _lifecycle.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    async Task RunLoopAsync(CancellationToken ct)
    {
        Exception? fault = null;
        try
        {
            await foreach (var request in _inner.AdvertiseAsync(ct))
            {
                LogConnectionRequested(request.RemoteDevice.Id, request.RemoteDevice.DisplayName);
                lock (_lifecycle.StateLock)
                {
                    _lifecycle.PendingSnapshot.Add(request);
                    _lifecycle.Publish(new AdvertiserEvent.ConnectionRequested(request));
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
                _lifecycle.Fault(fault);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> AcceptAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var conn = await request.AcceptAsync(cancellationToken);
        conn.Role = ConnectionRole.Acceptor;
        CancellationToken serviceToken;
        lock (_lifecycle.StateLock)
        {
            serviceToken = _lifecycle.CurrentServiceToken;
            _lifecycle.PendingSnapshot.Remove(request);
            _lifecycle.ActiveSnapshot.Add(conn);
            _lifecycle.Publish(new AdvertiserEvent.ConnectionAccepted(conn));
        }
        LogConnectionAccepted(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);

        _ = _lifecycle.MonitorConnectionAsync(
            conn,
            onDropped: c => new AdvertiserEvent.ConnectionDropped(c),
            logDropped: LogConnectionDropped,
            serviceToken);
        _ = _lifecycle.ForwardPayloadsAsync(
            conn,
            onPayload: (c, p) => new AdvertiserEvent.PayloadReceived(c, p),
            logError: LogForwardPayloadsError,
            serviceToken);

        return conn;
    }

    /// <inheritdoc/>
    public Task RejectAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default)
    {
        LogConnectionRejected(request.RemoteDevice.Id, request.RemoteDevice.DisplayName);
        lock (_lifecycle.StateLock)
        {
            _lifecycle.PendingSnapshot.Remove(request);
        }
        return request.RejectAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AdvertiserEvent> EventsAsync(CancellationToken cancellationToken = default)
    {
        return _lifecycle.EventsAsync(
            buildSnapshot: () => _lifecycle.PendingSnapshot.Select(r => (AdvertiserEvent)new AdvertiserEvent.ConnectionRequested(r))
                .Concat(_lifecycle.ActiveSnapshot.Select(c => new AdvertiserEvent.ConnectionAccepted(c))),
            synchronized: () => new AdvertiserEvent.Synchronized(),
            cancellationToken);
    }
}
