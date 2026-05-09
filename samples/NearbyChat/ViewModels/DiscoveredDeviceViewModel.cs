using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    NearbyDevice device,
    INearbyDiscoverer discoverer) : NearbyDeviceViewModel(device)
{
    public override string StateGlyph => Resource<string>("icon-magnify");
    public override Color StateColor => Resource<Color>("LightTextQuaternary");

    [RelayCommand]
    Task<NearbyConnection> Connect()
        => discoverer.ConnectAsync(Device);
}
