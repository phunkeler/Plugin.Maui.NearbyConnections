using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.ViewModels;

public abstract partial class NearbyDeviceViewModel : ObservableRecipient
{
    protected NearbyDevice Device { get; }

    public string Id => Device.Id;
    public string DisplayName => Device.DisplayName ?? "Unknown";
    public DateTimeOffset ReceivedAt { get; } = DateTimeOffset.UtcNow;
    public abstract string StateGlyph { get; }
    public abstract Color StateColor { get; }

    protected NearbyDeviceViewModel(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Device = device;
    }

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(ReceivedAt));

    protected static T Resource<T>(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var value) && value is T typed
            ? typed
            : default!;
}
