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
    async Task Accept()
    {
        await NearbyConnectionsService.RespondToConnectionAsync(Device, true);
    }

    [RelayCommand]
    async Task Decline()
    {
        await NearbyConnectionsService.RespondToConnectionAsync(Device, false);
    }
}