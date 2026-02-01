using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface INearbyConnectionsService : IDisposable
{
    bool IsAdvertising { get; }
    bool IsDiscovering { get; }

    IReadOnlyCollection<DiscoveredDevice> DiscoveredDevices { get; }

    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);
    Task RequestConnectionAsync(NearbyDevice device);
}

public partial class NearbyConnectionsService : INearbyConnectionsService
{
    readonly List<DiscoveredDevice> _discoveredDevices = [];

    readonly INearbyConnections _nearbyConnections;
    readonly IMessenger _messenger;

    bool _disposed;

    public bool IsAdvertising => _nearbyConnections.IsAdvertising;
    public bool IsDiscovering => _nearbyConnections.IsDiscovering;

    public IReadOnlyCollection<DiscoveredDevice> DiscoveredDevices
        => _discoveredDevices.AsReadOnly();

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

    void OnAdvertisingStateChanged(object? sender, AdvertisingStateChangedEventArgs e)
        => _messenger.Send(new AdvertisingStateChangedMessage(e.IsAdvertising));

    void OnDiscoveringStateChanged(object? sender, DiscoveringStateChangedEventArgs e)
        => _messenger.Send(new DiscoveringStateChangedMessage(e.IsDiscovering));

    void OnDeviceFound(object? sender, NearbyConnectionsEventArgs e)
    {
        if (_discoveredDevices.Any(d => d.Device.Id == e.NearbyDevice.Id))
            return;

        _discoveredDevices.Add(new DiscoveredDevice(e.NearbyDevice, e.Timestamp));
        _messenger.Send(new DeviceFoundMessage(e.NearbyDevice, e.Timestamp));
    }

    void OnDeviceLost(object? sender, NearbyConnectionsEventArgs e)
    {
        var discovered = _discoveredDevices.FirstOrDefault(d => d.Device.Id == e.NearbyDevice.Id);

        if (discovered is not null)
        {
            _discoveredDevices.Remove(discovered);
            _messenger.Send(new DeviceLostMessage(discovered.Device));
        }
    }

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
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
