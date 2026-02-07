using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    NearbyDevice device,
    INearbyConnectionsService nearbyConnectionsService,
    IDispatcher dispatcher) : NearbyDeviceViewModel(device, nearbyConnectionsService, dispatcher)
{
    [RelayCommand]
    async Task Connect()
    {
        await NearbyConnectionsService.RequestConnectionAsync(Device);
    }
}