using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.Services;

public sealed partial class ConnectionTracker : ObservableObject, IConnectionTracker, IAdvertiserHandler, IDiscovererHandler
{
    readonly IDispatcher _dispatcher;

    // All mutations run on the dispatcher (see the Dispatcher properties below), so no locking is needed.
    readonly HashSet<string> _connectedDeviceIds = [];

    [ObservableProperty]
    public partial int Count { get; private set; }

    IDispatcher? IAdvertiserHandler.Dispatcher => _dispatcher;
    IDispatcher? IDiscovererHandler.Dispatcher => _dispatcher;

    public ConnectionTracker(
        INearbyAdvertiser advertiser,
        INearbyDiscoverer discoverer,
        IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;

        // Singleton-lifetime subscriptions: both streams replay current-state events to new
        // subscribers, so the tracker is correct even if it is resolved after connections exist.
        _ = advertiser.EventsAsync().RunAsync(this);
        _ = discoverer.EventsAsync().RunAsync(this);
    }

    Task IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
        => Add(ev.Connection.RemoteDevice.Id);

    Task IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
        => Remove(ev.Connection.RemoteDevice.Id);

    Task IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
        => Add(ev.Connection.RemoteDevice.Id);

    Task IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
        => Remove(ev.Connection.RemoteDevice.Id);

    Task Add(string deviceId)
    {
        if (_connectedDeviceIds.Add(deviceId))
        {
            Count = _connectedDeviceIds.Count;
        }

        return Task.CompletedTask;
    }

    Task Remove(string deviceId)
    {
        if (_connectedDeviceIds.Remove(deviceId))
        {
            Count = _connectedDeviceIds.Count;
        }

        return Task.CompletedTask;
    }
}
