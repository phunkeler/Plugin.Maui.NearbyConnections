using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A discovered, not-yet-connected device row.
/// </summary>
public partial class DiscoveredDeviceViewModel : NearbyDeviceViewModel
{
    readonly INearbyConnections _session;

    public DiscoveredDeviceViewModel(NearbyDevice device, INearbyConnections session)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;

        // The device is observable, so the row tracks its status rather than maintaining a copy.
        Device.PropertyChanged += OnDevicePropertyChanged;
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
        catch (NearbyConnectionsException)
        {
            // Rejected or unreachable. The session has already returned the device to Visible.
        }
    }

    void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NearbyDevice.Status) or null)
        {
            OnPropertyChanged(nameof(IsConnecting));
        }
    }
}
