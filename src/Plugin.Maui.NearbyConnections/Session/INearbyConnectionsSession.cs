using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The second-most important consumable.
/// </summary>
public interface INearbyConnectionsSession : IDisposable
{
    /// <summary>
    /// Start advertising this device.
    /// </summary>
    /// <returns></returns>
    Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = null);

    /// <summary>
    /// Stop advertising this device.
    /// </summary>
    /// <returns></returns>
    void StopAdvertising();

    /// <summary>
    /// Begin discovery of <see cref="INearbyDevice"/>'s.
    /// </summary>
    /// <returns></returns>
    Task StartDiscoveryAsync(DiscoverOptions? discoverOptions = null);

    /// <summary>
    /// Stop discovering nearby devices.
    /// </summary>
    /// <returns></returns>
    void StopDiscovery();

    /// <summary>
    /// The provider of <see cref="INearbyConnectionsEvent"/> events.
    /// </summary>
    IObservable<INearbyConnectionsEvent> Events { get; }
}