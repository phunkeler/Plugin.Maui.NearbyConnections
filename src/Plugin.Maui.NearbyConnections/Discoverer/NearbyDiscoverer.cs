using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tier-2 discoverer service that manages the discovery lifecycle, nearby visible devices,
/// and active connections on the discoverer side.
/// </summary>
public partial class NearbyDiscoverer : INearbyDiscoverer, IDisposable
{
    readonly INearbyConnections _inner;
    readonly ILogger _logger;
    CancellationTokenSource? _cts;
    Channel<DiscovererEvent> _eventChannel = Channel.CreateUnbounded<DiscovererEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    readonly object _stateLock = new();
    readonly List<NearbyDevice> _visibleSnapshot = [];
    readonly List<NearbyConnection> _activeSnapshot = [];

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

    /// <inheritdoc/>
    public bool IsDiscovering { get; private set; }

    /// <inheritdoc/>
    public Task StartAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _eventChannel.Writer.TryComplete();
        _eventChannel = Channel.CreateUnbounded<DiscovererEvent>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        lock (_stateLock)
        {
            _visibleSnapshot.Clear();
            _activeSnapshot.Clear();
        }
        _cts = new CancellationTokenSource();
        IsDiscovering = true;
        LogDiscoveryStarted();
        _ = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        IsDiscovering = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _eventChannel.Writer.TryComplete();
        LogDiscoveryStopped();
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
            await foreach (var ev in _inner.DiscoverAsync(ct))
            {
                if (ev.Type == NearbyDeviceEventType.Found)
                {
                    LogDeviceFound(ev.Device.Id, ev.Device.DisplayName);
                    lock (_stateLock)
                    {
                        _visibleSnapshot.Add(ev.Device);
                    }
                    eventChannel.Writer.TryWrite(new DiscovererEvent.DeviceFound(ev.Device));
                }
                else
                {
                    LogDeviceLost(ev.Device.Id, ev.Device.DisplayName);
                    lock (_stateLock)
                    {
                        _visibleSnapshot.Remove(ev.Device);
                    }
                    eventChannel.Writer.TryWrite(new DiscovererEvent.DeviceLost(ev.Device));
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
            IsDiscovering = false;
            eventChannel.Writer.TryComplete(fault);
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
        lock (_stateLock)
        {
            _activeSnapshot.Add(conn);
        }
        LogConnected(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        var serviceToken = _cts?.Token ?? CancellationToken.None;
        var channel = _eventChannel;
        channel.Writer.TryWrite(new DiscovererEvent.DeviceConnected(conn));
        _ = MonitorConnectionAsync(conn, channel, serviceToken);
        _ = ForwardPayloadsAsync(conn, channel, serviceToken);
        return conn;
    }

    private async Task MonitorConnectionAsync(NearbyConnection conn, Channel<DiscovererEvent> channel, CancellationToken serviceToken)
    {
        try
        {
            await conn.Disconnected.WaitAsync(serviceToken);
            LogConnectionDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
            lock (_stateLock) { _activeSnapshot.Remove(conn); }
            channel.Writer.TryWrite(new DiscovererEvent.DeviceDisconnected(conn));
        }
        catch (OperationCanceledException)
        {
            lock (_stateLock) { _activeSnapshot.Remove(conn); }
        }
    }

    private static async Task ForwardPayloadsAsync(NearbyConnection conn, Channel<DiscovererEvent> channel, CancellationToken ct)
    {
        try
        {
            await foreach (var payload in conn.ReceiveAsync(ct))
            {
                channel.Writer.TryWrite(new DiscovererEvent.PayloadReceived(conn, payload));
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
    public async IAsyncEnumerable<DiscovererEvent> EventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IEnumerable<DiscovererEvent> snapshot;
        lock (_stateLock)
        {
            snapshot = _visibleSnapshot.Select(d => (DiscovererEvent)new DiscovererEvent.DeviceFound(d))
                .Concat(_activeSnapshot.Select(c => new DiscovererEvent.DeviceConnected(c)))
                .ToList();
        }

        foreach (var ev in snapshot)
        {
            yield return ev;
        }

        yield return new DiscovererEvent.Synchronized();

        await foreach (var ev in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return ev;
        }
    }
}
