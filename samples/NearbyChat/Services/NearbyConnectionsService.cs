using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface INearbyConnectionsService : INotifyPropertyChanged, IDisposable
{
    bool IsAdvertising { get; }
    bool IsDiscovering { get; }

    ObservableCollection<NearbyDevice> DiscoveredDevices { get; }

    INearbyConnections NearbyConnections { get; }

    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);

}

public partial class NearbyConnectionsService : ObservableObject, INearbyConnectionsService
{
    bool _disposed;

    [ObservableProperty]
    bool _isAdvertising;

    [ObservableProperty]
    bool _isDiscovering;

    [ObservableProperty]
    ObservableCollection<NearbyDevice> _discoveredDevices = [];

    public INearbyConnections NearbyConnections { get; }

    public NearbyConnectionsService(INearbyConnections nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        NearbyConnections = nearbyConnections;

        NearbyConnections.Events.AdvertisingStateChanged += OnAdvertisingStateChanged;
        NearbyConnections.Events.DiscoveringStateChanged += OnDiscoveringStateChanged;
        NearbyConnections.Events.DeviceFound += OnDeviceFound;
    }

    public Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
        => NearbyConnections.StartAdvertisingAsync(cancellationToken);

    public Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
        => NearbyConnections.StopAdvertisingAsync(cancellationToken);

    public Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
        => NearbyConnections.StartDiscoveryAsync(cancellationToken);

    public Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
        => NearbyConnections.StopDiscoveryAsync(cancellationToken);

    void OnAdvertisingStateChanged(object? sender, AdvertisingStateChangedEventArgs e)
        => IsAdvertising = e.IsAdvertising;

    void OnDiscoveringStateChanged(object? sender, DiscoveringStateChangedEventArgs e)
        => IsDiscovering = e.IsDiscovering;

    void OnDeviceFound(object? sender, NearbyConnectionsEventArgs e)
    {
        if (DiscoveredDevices.Any(d => d.Id == e.NearbyDevice.Id))
            return;

        DiscoveredDevices.Add(e.NearbyDevice);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            NearbyConnections.Events.AdvertisingStateChanged -= OnAdvertisingStateChanged;
            NearbyConnections.Events.DiscoveringStateChanged -= OnDiscoveringStateChanged;
            NearbyConnections.Events.DeviceFound -= OnDeviceFound;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
