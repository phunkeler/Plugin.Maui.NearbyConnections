using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tier-2 discoverer service that manages the discovery lifecycle, nearby visible devices,
/// and active connections on the discoverer side.
/// </summary>
public sealed partial class NearbyDiscoverer : INearbyDiscoverer
{
    readonly INearbyConnections _inner;
    readonly ILogger _logger;
    CancellationTokenSource? _cts;
    readonly List<Channel<DiscovererEvent>> _subscribers = [];
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

        // Emit DeviceLost for any previously visible devices so subscribers can clear their UI.
        lock (_stateLock)
        {
            foreach (var device in _visibleSnapshot)
            {
                Publish(new DiscovererEvent.DeviceLost(device));
            }
            _visibleSnapshot.Clear();
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

        // Emit DeviceLost for all visible devices — they are no longer reachable once scanning stops.
        lock (_stateLock)
        {
            foreach (var device in _visibleSnapshot)
            {
                Publish(new DiscovererEvent.DeviceLost(device));
            }
            _visibleSnapshot.Clear();
        }

        LogDiscoveryStopped();
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

    // Must be called inside _stateLock.
    void Publish(DiscovererEvent ev)
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
            await foreach (var ev in _inner.DiscoverAsync(ct))
            {
                if (ev.Type == NearbyDeviceEventType.Found)
                {
                    LogDeviceFound(ev.Device.Id, ev.Device.DisplayName);
                    lock (_stateLock)
                    {
                        _visibleSnapshot.Add(ev.Device);
                        Publish(new DiscovererEvent.DeviceFound(ev.Device));
                    }
                }
                else
                {
                    LogDeviceLost(ev.Device.Id, ev.Device.DisplayName);
                    lock (_stateLock)
                    {
                        _visibleSnapshot.Remove(ev.Device);
                        Publish(new DiscovererEvent.DeviceLost(ev.Device));
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
            IsDiscovering = false;
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
    public async Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        LogConnecting(device.Id, device.DisplayName);
        lock (_stateLock)
        {
            _visibleSnapshot.Remove(device);
        }
        var conn = await _inner.ConnectAsync(device, cancellationToken);
        conn.Role = ConnectionRole.Initiator;
        var serviceToken = _cts?.Token ?? CancellationToken.None;
        lock (_stateLock)
        {
            _activeSnapshot.Add(conn);
            Publish(new DiscovererEvent.DeviceConnected(conn));
        }
        LogConnected(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
        _ = MonitorConnectionAsync(conn, serviceToken);
        _ = ForwardPayloadsAsync(conn, serviceToken);
        return conn;
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
                Publish(new DiscovererEvent.DeviceDisconnected(conn));
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
                    Publish(new DiscovererEvent.PayloadReceived(conn, payload));
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
    public async IAsyncEnumerable<DiscovererEvent> EventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sub = Channel.CreateUnbounded<DiscovererEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        IEnumerable<DiscovererEvent> snapshot;

        lock (_stateLock)
        {
            snapshot = _visibleSnapshot.Select(d => (DiscovererEvent)new DiscovererEvent.DeviceFound(d))
                .Concat(_activeSnapshot.Select(c => new DiscovererEvent.DeviceConnected(c)))
                .ToList();
            _subscribers.Add(sub);
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
            lock (_stateLock) { _subscribers.Remove(sub); }
            sub.Writer.TryComplete();
        }
    }
}
