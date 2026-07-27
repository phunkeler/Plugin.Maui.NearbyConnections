using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class NearbyDeviceViewModel(NearbyDevice device) : ObservableObject
{
    protected NearbyDevice Device { get; } = device ?? throw new ArgumentNullException(nameof(device));

    public string Id => Device.Id;
    public string DisplayName => Device.DisplayName ?? "Unknown";
    public DateTimeOffset ReceivedAt { get; } = DateTimeOffset.UtcNow;

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(ReceivedAt));
}
