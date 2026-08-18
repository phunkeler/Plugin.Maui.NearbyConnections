using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A connected device row.
/// </summary>
/// <remarks>
/// Takes the session rather than a <see cref="NearbyConnection"/>, so it can be projected from a
/// <see cref="NearbyDeviceCollection{TRow}"/> like every other row type. The connection is looked
/// up per command instead of captured: a device is a value and cannot hold a live handle, and a
/// captured handle would go stale the moment the peer dropped and reconnected.
/// </remarks>
public partial class ConnectedDeviceViewModel(
    NearbyDevice device,
    INearby session,
    IBottomSheetNavigationService bottomSheetNavigationService) : NearbyDeviceViewModel(device)
{
    [RelayCommand]
    Task<INavigationResult> Chat()
        => bottomSheetNavigationService.NavigateToAsync(nameof(ChatViewModel), new BottomSheetNavigationParameters
        {
            { nameof(NearbyDevice), Device }
        });

    [RelayCommand]
    async Task Disconnect()
    {
        // Already gone if the lookup fails — the row is about to leave the collection anyway,
        // because the device stops matching the Connected filter.
        if (session.TryGetConnection(Device.Id, out var connection))
        {
            await connection.DisposeAsync();
        }
    }
}
