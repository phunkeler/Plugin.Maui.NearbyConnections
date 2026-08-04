using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A discovered, not-yet-connected device row.
/// </summary>
/// <remarks>
/// The old <c>IsConnecting</c> property and its manual unwind on failure are gone:
/// <see cref="NearbyDevice.Status"/> carries that state and the session resets it if the handshake
/// fails, so the row cannot get stuck spinning.
/// </remarks>
public partial class DiscoveredDeviceViewModel(
    NearbyDevice device,
    INearbySession session) : NearbyDeviceViewModel(device)
{
    [RelayCommand]
    async Task Connect()
    {
        try
        {
            await session.ConnectAsync(Device);
        }
        catch (NearbyConnectionsException)
        {
            // Rejected or unreachable. The session has already returned the device to Visible.
        }
    }
}
