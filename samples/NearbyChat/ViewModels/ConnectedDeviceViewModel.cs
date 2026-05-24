using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectedDeviceViewModel(
    NearbyDevice device,
    INearbyConnectionsService nearbyConnectionsService,
    IBottomSheetNavigationService bottomSheetNavigationService) : NearbyDeviceViewModel(device, nearbyConnectionsService)
{
    [RelayCommand]
    Task<INavigationResult> Chat()
        => bottomSheetNavigationService.NavigateToAsync(nameof(ChatViewModel), new BottomSheetNavigationParameters
        {
            { nameof(NearbyDevice), Device }
        });

    [RelayCommand]
    Task Disconnect()
        => NearbyConnectionsService.DisconnectAsync(Device);
}