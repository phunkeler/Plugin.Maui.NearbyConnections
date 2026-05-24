using NearbyChat.ViewModels;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface INearbyDeviceViewModelFactory
{
    AdvertisedDeviceViewModel CreateAdvertiser(NearbyDevice device);
    DiscoveredDeviceViewModel CreateDiscoverer(NearbyDevice device);
    ConnectedDeviceViewModel CreateConnected(NearbyDevice device);
}

public class NearbyDeviceViewModelFactory(
    INearbyConnectionsService nearbyConnectionsService,
    IBottomSheetNavigationService bottomSheetNavigationService) : INearbyDeviceViewModelFactory
{
    public AdvertisedDeviceViewModel CreateAdvertiser(NearbyDevice device)
        => new(device, nearbyConnectionsService);

    public DiscoveredDeviceViewModel CreateDiscoverer(NearbyDevice device)
        => new(device, nearbyConnectionsService);

    public ConnectedDeviceViewModel CreateConnected(NearbyDevice device)
        => new(device, nearbyConnectionsService, bottomSheetNavigationService);
}
