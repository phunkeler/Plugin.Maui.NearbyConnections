using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.ViewModels;

public partial class ConnectedDeviceViewModel(
    NearbyConnection connection,
    IBottomSheetNavigationService bottomSheetNavigationService) : NearbyDeviceViewModel(connection.RemoteDevice)
{
    [RelayCommand]
    Task<INavigationResult> Chat()
        => bottomSheetNavigationService.NavigateToAsync(nameof(ChatViewModel), new BottomSheetNavigationParameters
        {
            { nameof(NearbyDevice), Device }
        });

    [RelayCommand]
    Task Disconnect()
        => connection.DisposeAsync().AsTask();
}
