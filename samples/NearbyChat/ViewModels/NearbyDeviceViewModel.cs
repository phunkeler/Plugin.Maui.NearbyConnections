using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class NearbyDeviceViewModel : ObservableObject
{
    NearbyDevice _device;

    protected NearbyDeviceViewModel(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        _device = device;
    }

    /// <summary>
    /// The device this row shows.
    /// </summary>
    /// <remarks>
    /// A device is an immutable snapshot, so a row updates by being handed a newer one rather than
    /// by watching the device for property changes. That is what retires the
    /// <c>Device.PropertyChanged</c> subscription this type used to take and never release.
    /// </remarks>
    protected NearbyDevice Device
    {
        get => _device;
        private set
        {
            ArgumentNullException.ThrowIfNull(value);

            _device = value;
            OnPropertyChanged(nameof(DisplayName));
            OnDeviceChanged();
        }
    }

    public string Id => Device.Id;
    public string DisplayName => Device.DisplayName ?? "Unknown";
    public DateTimeOffset ReceivedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Replaces the snapshot this row shows. Ignores a device with a different identity, so a
    /// mis-keyed update cannot silently turn one row into another.
    /// </summary>
    public void Update(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (string.Equals(device.Id, Device.Id, StringComparison.Ordinal))
        {
            Device = device;
        }
    }

    /// <summary>
    /// Re-runs the relative-time binding on this row.
    /// </summary>
    /// <remarks>
    /// <see cref="ReceivedAt"/> never changes. The notification exists to make the binding re-run
    /// its time-dependent converter, so "2 minutes ago" advances as the clock does.
    /// </remarks>
    public void RefreshRelativeTime() => OnPropertyChanged(nameof(ReceivedAt));

    /// <summary>
    /// Called after <see cref="Device"/> is replaced, for rows with derived properties to raise.
    /// </summary>
    protected virtual void OnDeviceChanged()
    {
    }
}
