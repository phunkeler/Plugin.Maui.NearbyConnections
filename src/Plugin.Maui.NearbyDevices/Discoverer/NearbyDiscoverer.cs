using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyDevices;


/// <summary>
/// Tier-2 discoverer service that manages the discovery lifecycle, nearby visible devices,
/// and active connections on the discoverer side.
/// </summary>
public sealed partial class NearbyDiscoverer : INearbyDiscoverer
{
    readonly INearbyDevices _inner;
    readonly ILogger _logger;
    CancellationTokenSource? _cts;
    readonly ChannelBroadcaster<DiscovererEvent> _broadcaster = new();
    readonly Lock _stateLock = new();
    readonly List<NearbyDevice> _visibleSnapshot = [];
    readonly List<NearbyConnection> _activeSnapshot = [];

    /// <summary>
    /// Initializes a new <see cref="NearbyDiscoverer"/>.
    /// </summary>
    /// <param name="inner">The underlying <see cref="INearbyDevices"/> implementation.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}"/> when not provided.</param>
    public NearbyDiscoverer(INearbyDevices inner, ILogger<NearbyDiscoverer>? logger = null)
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
        CancellationTokenSource cts;
        lock (_stateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            foreach (var device in _visibleSnapshot)
            {
                _broadcaster.Publish(new DiscovererEvent.DeviceLost(device));
            }
            _visibleSnapshot.Clear();
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        _isDiscovering = true;
        LogDiscoveryStarted();
        _ = RunLoopAsync(cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        _isDiscovering = false;
        lock (_stateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            foreach (var device in _visibleSnapshot)
            {
                _broadcaster.Publish(new DiscovererEvent.DeviceLost(device));
            }
            _visibleSnapshot.Clear();
        }

        LogDiscoveryStopped();
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
                    lock (_stateLock)
                    {
                        _visibleSnapshot.Add(ev.Device);
                        _broadcaster.Publish(new DiscovererEvent.DeviceFound(ev.Device));
                    }
                }
                else
                {
                    LogDeviceLost(ev.Device.Id, ev.Device.DisplayName);
                    lock (_stateLock)
                    {
                        _visibleSnapshot.Remove(ev.Device);
                        _broadcaster.Publish(new DiscovererEvent.DeviceLost(ev.Device));
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
                lock (_stateLock)
                {
                    _broadcaster.Complete(fault);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        LogConnecting(device.Id, device.DisplayName);
        lock (_stateLock)
        {
            _visibleSnapshot.Remove(device);
        }
        var conn = await _inner.ConnectAsync(device, cancellationToken);
        conn.Role = ConnectionRole.Initiator;
        CancellationToken serviceToken;
        lock (_stateLock)
        {
            serviceToken = _cts?.Token ?? CancellationToken.None;
            _activeSnapshot.Add(conn);
            _broadcaster.Publish(new DiscovererEvent.DeviceConnected(conn));
        }
        LogConnected(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        _ = MonitorConnectionAsync(conn, serviceToken);
        _ = ForwardPayloadsAsync(conn, serviceToken);
        return conn;
    }

    async Task MonitorConnectionAsync(NearbyConnection conn, CancellationToken serviceToken)
    {
        try
        {
            await conn.Disconnected.WaitAsync(serviceToken);
            LogConnectionDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
            lock (_stateLock)
            {
                _activeSnapshot.Remove(conn);
                _broadcaster.Publish(new DiscovererEvent.DeviceDisconnected(conn));
            }
        }
        catch (OperationCanceledException)
        {
            // serviceToken is cancelled by StopAsync(), not by the connection
            // itself dropping - but from a subscriber's perspective (e.g.
            // ConnectionsPageViewModel.ConnectedDevices, built by replaying
            // EventsAsync's snapshot + live events) this connection is gone
            // either way. Removing it from _activeSnapshot without publishing
            // DeviceDisconnected meant a subscriber that already added this
            // connection before the stop never learned it should remove it -
            // it would silently stay in a UI collection forever, even though
            // it no longer appears in any future EventsAsync snapshot replay.
            // Confirmed via observed "5+ connections" accumulating in the
            // Connections page across repeated discover/stop cycles.
            LogConnectionDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
            lock (_stateLock)
            {
                _activeSnapshot.Remove(conn);
                _broadcaster.Publish(new DiscovererEvent.DeviceDisconnected(conn));
            }
        }
    }

    async Task ForwardPayloadsAsync(NearbyConnection conn, CancellationToken ct)
    {
        try
        {
            await foreach (var payload in conn.ReceiveAsync(ct))
            {
                lock (_stateLock)
                {
                    _broadcaster.Publish(new DiscovererEvent.PayloadReceived(conn, payload));
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
    public async IAsyncEnumerable<DiscovererEvent> EventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<DiscovererEvent> sub;
        IEnumerable<DiscovererEvent> snapshot;

        lock (_stateLock)
        {
            snapshot = _visibleSnapshot.Select(d => (DiscovererEvent)new DiscovererEvent.DeviceFound(d))
                .Concat(_activeSnapshot.Select(c => new DiscovererEvent.DeviceConnected(c)))
                .ToList();
            sub = _broadcaster.Subscribe();
        }

        try
        {
            foreach (var ev in snapshot)
            {
                yield return ev;
            }

            yield return new DiscovererEvent.Synchronized();

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
