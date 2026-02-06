using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisedDeviceViewModel(
    NearbyDevice device,
    INearbyConnectionsService nearbyConnectionsService) : NearbyDeviceViewModel(device)
{
    public DateTimeOffset LastSeenAt { get; } = device.LastSeenAt;

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(LastSeenAt));

    [RelayCommand]
    async Task Accept()
    {
        await nearbyConnectionsService.RespondToConnectionAsync(Device, true);
    }

    [RelayCommand]
    async Task Decline()
    {
        await nearbyConnectionsService.RespondToConnectionAsync(Device, false);
    }
}