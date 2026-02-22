using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface INearbyConnectionsService : IDisposable
{
    IReadOnlyList<NearbyDevice> Devices { get; }
    bool IsAdvertising { get; }
    bool IsDiscovering { get; }

    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);
    Task RequestConnectionAsync(NearbyDevice device);
    Task RespondToConnectionAsync(NearbyDevice device, bool accept);
    Task SendMessage(NearbyDevice device, string message);
    Task SendAsync(
        NearbyDevice device,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public partial class NearbyConnectionsService : INearbyConnectionsService
{
    readonly INearbyConnections _nearbyConnections;
    readonly IMessenger _messenger;

    bool _disposed;

    public IReadOnlyList<NearbyDevice> Devices => _nearbyConnections.Devices;
    public bool IsAdvertising => _nearbyConnections.IsAdvertising;
    public bool IsDiscovering => _nearbyConnections.IsDiscovering;

    public NearbyConnectionsService(
        INearbyConnections nearbyConnections,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);
        ArgumentNullException.ThrowIfNull(messenger);

        _nearbyConnections = nearbyConnections;
        _messenger = messenger;

        _nearbyConnections.Events.AdvertisingStateChanged += OnAdvertisingStateChanged;
        _nearbyConnections.Events.DiscoveringStateChanged += OnDiscoveringStateChanged;
        _nearbyConnections.Events.DeviceFound += OnDeviceFound;
        _nearbyConnections.Events.DeviceLost += OnDeviceLost;
        _nearbyConnections.Events.ConnectionRequested += OnConnectionRequested;
        _nearbyConnections.Events.DeviceStateChanged += OnDeviceStateChanged;
        _nearbyConnections.Events.ConnectionResponded += OnConnectionResponded;
        _nearbyConnections.Events.DeviceDisconnected += OnDeviceDisconnected;
        _nearbyConnections.Events.DataReceived += OnDataReceived;
    }

    public Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
        => _nearbyConnections.StartAdvertisingAsync(cancellationToken);

    public Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
        => _nearbyConnections.StopAdvertisingAsync(cancellationToken);

    public Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
        => _nearbyConnections.StartDiscoveryAsync(cancellationToken);

    public Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
        => _nearbyConnections.StopDiscoveryAsync(cancellationToken);

    public Task RequestConnectionAsync(NearbyDevice device)
        => _nearbyConnections.RequestConnectionAsync(device);

    public Task RespondToConnectionAsync(NearbyDevice device, bool accept)
        => _nearbyConnections.RespondToConnectionAsync(device, accept);

    public Task SendMessage(NearbyDevice device, string message)
        => _nearbyConnections.SendAsync(device, Encoding.UTF8.GetBytes(message));

    public Task SendAsync(
        NearbyDevice device,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => _nearbyConnections.SendAsync(
            device,
            uri,
            progress,
            cancellationToken);

    void OnAdvertisingStateChanged(object? sender, AdvertisingStateChangedEventArgs e)
        => _messenger.Send(new AdvertisingStateChangedMessage(e.IsAdvertising));

    void OnDiscoveringStateChanged(object? sender, DiscoveringStateChangedEventArgs e)
        => _messenger.Send(new DiscoveringStateChangedMessage(e.IsDiscovering));

    void OnDeviceFound(object? sender, NearbyConnectionsEventArgs e)
    {
        _messenger.Send(new DeviceFoundMessage(e.NearbyDevice, e.Timestamp));
    }

    void OnDeviceLost(object? sender, NearbyConnectionsEventArgs e)
    {
        _messenger.Send(new DeviceLostMessage(e.NearbyDevice));
    }

    void OnConnectionRequested(object? sender, NearbyConnectionsEventArgs e)
    {
        _messenger.Send(new ConnectionRequestMessage(e.NearbyDevice, e.Timestamp));
    }

    void OnDeviceStateChanged(object? sender, NearbyDeviceStateChangedEventArgs e)
    {
        _messenger.Send(new DeviceStateChangedMessage(e.NearbyDevice));
    }

    void OnConnectionResponded(object? sender, NearbyDeviceRespondedEventArgs e)
        => _messenger.Send(new ConnectionResponseMessage(e.NearbyDevice, e.Accepted));

    void OnDeviceDisconnected(object? sender, NearbyConnectionsEventArgs e)
        => _messenger.Send(new DeviceDisconnectedMessage(e.NearbyDevice));

    void OnDataReceived(object? sender, DataReceivedEventArgs e)
        => _messenger.Send(new DataReceivedMessage(e.NearbyDevice, e.Timestamp, e.Payload));


    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _nearbyConnections.Events.AdvertisingStateChanged -= OnAdvertisingStateChanged;
            _nearbyConnections.Events.DiscoveringStateChanged -= OnDiscoveringStateChanged;
            _nearbyConnections.Events.DeviceFound -= OnDeviceFound;
            _nearbyConnections.Events.DeviceLost -= OnDeviceLost;
            _nearbyConnections.Events.ConnectionRequested -= OnConnectionRequested;
            _nearbyConnections.Events.DeviceStateChanged -= OnDeviceStateChanged;
            _nearbyConnections.Events.ConnectionResponded -= OnConnectionResponded;
            _nearbyConnections.Events.DeviceDisconnected -= OnDeviceDisconnected;
            _nearbyConnections.Events.DataReceived -= OnDataReceived;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
