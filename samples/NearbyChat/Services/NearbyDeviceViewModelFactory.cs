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
    IDispatcher dispatcher,
    INearbyConnectionsService nearbyConnectionsService,
    IBottomSheetNavigationService bottomSheetNavigationService) : INearbyDeviceViewModelFactory
{
    public AdvertisedDeviceViewModel CreateAdvertiser(NearbyDevice device)
        => new(device, nearbyConnectionsService, dispatcher);

    public DiscoveredDeviceViewModel CreateDiscoverer(NearbyDevice device)
        => new(device, nearbyConnectionsService, dispatcher);

    public ConnectedDeviceViewModel CreateConnected(NearbyDevice device)
        => new(device, nearbyConnectionsService, dispatcher, bottomSheetNavigationService);
}