using CommunityToolkit.Mvvm.Input;
using NearbyChat.Models;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class AdvertisedDeviceViewModel(
    AdvertisedDevice advertisedDevice,
    INearbyConnectionsService nearbyConnectionsService) : NearbyDeviceViewModel(advertisedDevice.Device)
{
    public DateTimeOffset InvitedAt { get; } = advertisedDevice.InvitedAt.ToLocalTime();

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(InvitedAt));

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