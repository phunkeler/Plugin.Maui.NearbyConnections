using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class NearbyDeviceViewModel : ObservableObject
{
    protected NearbyDevice Device { get; }

    public string Id => Device.Id;
    public string DisplayName => Device.DisplayName ?? "Unknown";

    public NearbyDeviceViewModel(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        Device = device;
    }
}
