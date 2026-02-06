using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    NearbyDevice device,
    INearbyConnectionsService nearbyConnectionsService) : NearbyDeviceViewModel(device)
{
    public DateTimeOffset LastSeenAt { get; } = device.LastSeenAt;

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(LastSeenAt));

    [RelayCommand]
    async Task Connect()
    {
        await nearbyConnectionsService.RequestConnectionAsync(Device);
    }
}