using CommunityToolkit.Mvvm.Input;
using NearbyChat.Models;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    DiscoveredDevice discoveredDevice,
    INearbyConnectionsService nearbyConnectionsService) : NearbyDeviceViewModel(discoveredDevice.Device)
{
    public DateTimeOffset FoundAt { get; } = discoveredDevice.FoundAt.ToLocalTime();

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(FoundAt));

    [RelayCommand]
    async Task Connect()
    {
        await nearbyConnectionsService.RequestConnectionAsync(Device);
    }
}