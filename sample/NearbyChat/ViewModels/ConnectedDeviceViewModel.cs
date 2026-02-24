using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectedDeviceViewModel(
    NearbyDevice device,
    INearbyConnectionsService nearbyConnectionsService,
    IDispatcher dispatcher,
    IBottomSheetNavigationService bottomSheetNavigationService) : NearbyDeviceViewModel(device, nearbyConnectionsService, dispatcher)
{
    [RelayCommand]
    Task<INavigationResult> Chat()
        => bottomSheetNavigationService.NavigateToAsync(nameof(ChatViewModel), new BottomSheetNavigationParameters
        {
            { nameof(NearbyDevice), Device }
        });
}