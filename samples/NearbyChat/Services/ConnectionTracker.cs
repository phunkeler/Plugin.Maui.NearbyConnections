using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

/// <summary>
/// Exposes how many devices are currently connected, for the header chip.
/// </summary>
/// <remarks>
/// A singleton that lives as long as the session, so it subscribes without unsubscribing on
/// purpose — unlike a page ViewModel, which must use
/// <c>BasePageViewModel.RegisterSessionSubscription</c>.
/// </remarks>
public sealed partial class ConnectionTracker : ObservableObject, IConnectionTracker
{
    readonly INearby _session;

    [ObservableProperty]
    public partial int Count { get; private set; }

    public ConnectionTracker(INearby session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;

        // Devices is the state, so the count is derived rather than tracked. This used to be a
        // HashSet maintained by implementing both handler interfaces; the session already knows.
        ((INotifyCollectionChanged)_session.Devices).CollectionChanged += OnDevicesChanged;

        foreach (var device in _session.Devices)
        {
            device.PropertyChanged += OnDevicePropertyChanged;
        }

        Recount();
    }

    void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A device's Status changes in place, so the count depends on per-device notifications too,
        // not just on membership.
        foreach (var device in e.OldItems?.OfType<NearbyDevice>() ?? [])
        {
            device.PropertyChanged -= OnDevicePropertyChanged;
        }

        foreach (var device in e.NewItems?.OfType<NearbyDevice>() ?? [])
        {
            device.PropertyChanged += OnDevicePropertyChanged;
        }

        Recount();
    }

    void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NearbyDevice.Status) or null)
        {
            Recount();
        }
    }

    void Recount()
        => Count = _session.Devices.Count(d => d.Status is NearbyDeviceStatus.Connected);
}
