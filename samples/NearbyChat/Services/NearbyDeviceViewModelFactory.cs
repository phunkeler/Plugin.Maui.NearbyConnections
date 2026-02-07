using NearbyChat.ViewModels;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface INearbyDeviceViewModelFactory
{
    AdvertisedDeviceViewModel CreateAdvertiser(NearbyDevice device);
    DiscoveredDeviceViewModel CreateDiscoverer(NearbyDevice device);
}

public class NearbyDeviceViewModelFactory(
    IDispatcher dispatcher,
    INearbyConnectionsService nearbyConnectionsService) : INearbyDeviceViewModelFactory
{
    public AdvertisedDeviceViewModel CreateAdvertiser(NearbyDevice device)
        => new(device, nearbyConnectionsService, dispatcher);

    public DiscoveredDeviceViewModel CreateDiscoverer(NearbyDevice device)
        => new(device, nearbyConnectionsService, dispatcher);
}