using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A device awaiting a response to its inbound connection request.
/// </summary>
public partial class AdvertisedDeviceViewModel(
    NearbyDevice device,
    INearbySession session) : NearbyDeviceViewModel(device)
{
    [RelayCommand]
    Task<NearbyConnection> Accept()
        => session.AcceptAsync(Device);

    [RelayCommand]
    Task Decline()
        => session.RejectAsync(Device);
}
