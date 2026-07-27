using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections;


/// <summary>
/// Tier-2 discoverer service that manages the discovery lifecycle, nearby visible devices,
/// and active connections on the discoverer side.
/// </summary>
public sealed partial class NearbyDiscoverer : INearbyDiscoverer
{
    readonly INearbyConnections _inner;
    readonly ILogger _logger;
    readonly ConnectionLifecycle<NearbyDevice, DiscovererEvent> _lifecycle = new();

    /// <summary>
    /// Initializes a new <see cref="NearbyDiscoverer"/>.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyConnections"/> implementation.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyDiscoverer(INearbyConnections inner, ILogger<NearbyDiscoverer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _logger = logger ?? NullLogger<NearbyDiscoverer>.Instance;
    }

    volatile bool _isDiscovering;

    /// <inheritdoc/>
    public bool IsDiscovering => _isDiscovering;

    /// <inheritdoc/>
    public Task StartAsync()
    {
        return _lifecycle.StartAsync(
            executeTaskFactory: RunLoopAsync,
            onPendingExpired: device => new DiscovererEvent.DeviceLost(device),
            setRunningFlag: v =>
            {
                _isDiscovering = v;
                if (v)
                {
                    LogDiscoveryStarted();
                }
            });
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        return _lifecycle.StopAsync(
            onPendingExpired: device => new DiscovererEvent.DeviceLost(device),
            setRunningFlag: v =>
            {
                _isDiscovering = v;
                if (!v)
                {
                    LogDiscoveryStopped();
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
            await foreach (var ev in _inner.DiscoverAsync(ct))
            {
                if (ev.Type == NearbyDeviceEventType.Found)
                {
                    LogDeviceFound(ev.Device.Id, ev.Device.DisplayName);
                    lock (_lifecycle.StateLock)
                    {
                        _lifecycle.PendingSnapshot.Add(ev.Device);
                        _lifecycle.Publish(new DiscovererEvent.DeviceFound(ev.Device));
                    }
                }
                else
                {
                    LogDeviceLost(ev.Device.Id, ev.Device.DisplayName);
                    lock (_lifecycle.StateLock)
                    {
                        _lifecycle.PendingSnapshot.Remove(ev.Device);
                        _lifecycle.Publish(new DiscovererEvent.DeviceLost(ev.Device));
                    }
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
            _isDiscovering = false;
            if (fault is not null)
            {
                _lifecycle.Fault(fault);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        LogConnecting(device.Id, device.DisplayName);
        lock (_lifecycle.StateLock)
        {
            _lifecycle.PendingSnapshot.Remove(device);
        }
        var conn = await _inner.ConnectAsync(device, cancellationToken);
        conn.Role = ConnectionRole.Initiator;
        CancellationToken serviceToken;
        lock (_lifecycle.StateLock)
        {
            serviceToken = _lifecycle.CurrentServiceToken;
            _lifecycle.ActiveSnapshot.Add(conn);
            _lifecycle.Publish(new DiscovererEvent.DeviceConnected(conn));
        }
        LogConnected(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        _ = _lifecycle.MonitorConnectionAsync(
            conn,
            onDropped: c => new DiscovererEvent.DeviceDisconnected(c),
            logDropped: LogConnectionDropped,
            serviceToken);
        _ = _lifecycle.ForwardPayloadsAsync(
            conn,
            onPayload: (c, p) => new DiscovererEvent.PayloadReceived(c, p),
            logError: LogForwardPayloadsError,
            serviceToken);
        return conn;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<DiscovererEvent> EventsAsync(CancellationToken cancellationToken = default)
    {
        return _lifecycle.EventsAsync(
            buildSnapshot: () => _lifecycle.PendingSnapshot.Select(d => (DiscovererEvent)new DiscovererEvent.DeviceFound(d))
                .Concat(_lifecycle.ActiveSnapshot.Select(c => new DiscovererEvent.DeviceConnected(c))),
            synchronized: () => new DiscovererEvent.Synchronized(),
            cancellationToken);
    }
}
