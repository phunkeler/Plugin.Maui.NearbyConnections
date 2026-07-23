using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.ViewModels;

public partial class AdvertisedDeviceViewModel(
    NearbyConnectionRequest request,
    INearbyAdvertiser advertiser) : NearbyDeviceViewModel(request.RemoteDevice)
{
    public override string StateGlyph => Resource<string>("icon-down");
    public override Color StateColor => Resource<Color>("LightTextQuaternary");

    [RelayCommand]
    Task<NearbyConnection> Accept()
        => advertiser.AcceptAsync(request);

    [RelayCommand]
    Task Decline()
        => advertiser.RejectAsync(request);
}