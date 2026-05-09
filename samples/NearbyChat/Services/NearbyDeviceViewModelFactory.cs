using NearbyChat.ViewModels;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface INearbyDeviceViewModelFactory
{
    AdvertisedDeviceViewModel CreateAdvertiser(NearbyConnectionRequest request);
    DiscoveredDeviceViewModel CreateDiscoverer(NearbyDevice device);
    ConnectedDeviceViewModel CreateConnected(NearbyConnection connection);
}

public class NearbyDeviceViewModelFactory(
    INearbyAdvertiser advertiser,
    INearbyDiscoverer discoverer,
    IBottomSheetNavigationService bottomSheetNavigationService) : INearbyDeviceViewModelFactory
{
    public AdvertisedDeviceViewModel CreateAdvertiser(NearbyConnectionRequest request)
        => new(request, advertiser);

    public DiscoveredDeviceViewModel CreateDiscoverer(NearbyDevice device)
        => new(device, discoverer);

    public ConnectedDeviceViewModel CreateConnected(NearbyConnection connection)
        => new(connection, bottomSheetNavigationService);
}
