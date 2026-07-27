using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisedDeviceViewModel(
    NearbyConnectionRequest request,
    INearbyAdvertiser advertiser) : NearbyDeviceViewModel(request.RemoteDevice)
{
    [RelayCommand]
    Task<NearbyConnection> Accept()
        => advertiser.AcceptAsync(request);

    [RelayCommand]
    Task Decline()
        => advertiser.RejectAsync(request);
}