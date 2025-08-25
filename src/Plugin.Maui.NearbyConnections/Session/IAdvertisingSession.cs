using Plugin.Maui.NearbyConnections.Advertise;

namespace Plugin.Maui.NearbyConnections.Session;

public interface IAdvertisingSession : IDisposable
{
    event EventHandler<AdvertisingStateChangedEventArgs> StateChanged;

    bool IsActive { get; }

    Task StopAsync();
}
