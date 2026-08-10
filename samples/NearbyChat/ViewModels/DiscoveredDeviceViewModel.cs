using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A discovered, not-yet-connected device row.
/// </summary>
public partial class DiscoveredDeviceViewModel : NearbyDeviceViewModel
{
    readonly INearby _session;

    public DiscoveredDeviceViewModel(NearbyDevice device, INearby session)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
    }

    /// <summary>
    /// Whether a handshake is in flight, projected from <see cref="NearbyDevice.Status"/>.
    /// </summary>
    /// <remarks>
    /// No longer a settable flag with manual unwind on every failure path: the session owns the
    /// transition and resets the device if the handshake fails, so the row cannot get stuck
    /// spinning after a rejected connection.
    /// </remarks>
    public bool IsConnecting => Device.Status is NearbyDeviceStatus.Connecting;

    [RelayCommand]
    async Task Connect()
    {
        try
        {
            await _session.ConnectAsync(Device);
        }
        catch (NearbyException)
        {
            // Rejected or unreachable. The session has already returned the device to Visible.
        }
    }

    protected override void OnDeviceChanged() => OnPropertyChanged(nameof(IsConnecting));
}
