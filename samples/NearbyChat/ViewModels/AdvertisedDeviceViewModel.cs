using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A device awaiting a response to its inbound connection request.
/// </summary>
public partial class AdvertisedDeviceViewModel(
    NearbyDevice device,
    INearby session) : NearbyDeviceViewModel(device)
{
    [RelayCommand(IncludeCancelCommand = true)]
    Task<NearbyConnection> Accept(CancellationToken cancellationToken)
        => session.AcceptAsync(Device, cancellationToken);

    [RelayCommand]
    Task Decline()
        => session.RejectAsync(Device);
}
