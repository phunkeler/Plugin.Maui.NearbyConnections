using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The high-level options.
/// </summary>
public class NearbyConnectionsSessionOptions
{
    /// <summary>
    /// The options that control advertising.
    /// </summary>
    public AdvertiseOptions AdvertiseOptions { get; } = new();

    /// <summary>
    /// The options that control discovery.
    /// </summary>
    public DiscoverOptions DiscoverOptions { get; } = new();
}