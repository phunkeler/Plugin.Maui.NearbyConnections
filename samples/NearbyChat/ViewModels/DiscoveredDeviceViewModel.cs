using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    DiscoveredDevice device,
    INearbyConnectionsService nearbyConnectionsService) : NearbyDeviceViewModel(device.Device)
{
    public DateTimeOffset FoundAt { get; } = device.FoundAt.ToLocalTime();

    [RelayCommand]
    async Task Connect()
    {
        await nearbyConnectionsService.RequestConnectionAsync(Device);
    }
}