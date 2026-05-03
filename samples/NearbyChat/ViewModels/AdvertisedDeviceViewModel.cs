using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisedDeviceViewModel(
    NearbyDevice device,
    INearbyConnectionsService nearbyConnectionsService,
    IDispatcher dispatcher) : NearbyDeviceViewModel(device, nearbyConnectionsService, dispatcher)
{
    [RelayCommand]
    Task Accept()
        => NearbyConnectionsService.RespondToConnectionAsync(Device, true);

    [RelayCommand]
    Task Decline()
        => NearbyConnectionsService.RespondToConnectionAsync(Device, false);
}