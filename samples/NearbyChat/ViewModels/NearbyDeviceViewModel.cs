using CommunityToolkit.Mvvm.ComponentModel;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class NearbyDeviceViewModel : ObservableRecipient
{
    protected NearbyDevice Device { get; }
    protected INearbyConnectionsService NearbyConnectionsService { get; }

    public string Id => Device.Id;
    public string DisplayName => Device.DisplayName ?? "Unknown";
    public DateTimeOffset LastSeen => Device.LastSeen;
    public NearbyDeviceState State => Device.State;

    protected NearbyDeviceViewModel(
        NearbyDevice device,
        INearbyConnectionsService nearbyConnectionsService)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        Device = device;
        NearbyConnectionsService = nearbyConnectionsService;

        device.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NearbyDevice.State))
                OnPropertyChanged(nameof(State));
        };
    }

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(LastSeen));
}
