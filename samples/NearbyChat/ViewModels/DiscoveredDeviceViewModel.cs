using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    NearbyDevice device,
    INearbyConnectionsService nearbyConnectionsService) : NearbyDeviceViewModel(device, nearbyConnectionsService)
{
    [RelayCommand]
    Task Connect()
        => NearbyConnectionsService.RequestConnectionAsync(Device);
}