using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Session;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides access to the Nearby Connections plugin functionality.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    /// Gets the current <see cref="IAdvertisingSession"/> instance.
    /// </summary>
    IAdvertiser Advertiser { get; }

    /// <summary>
    /// Gets the current <see cref="IDiscoverer"/> instance.
    /// </summary>
    IDiscoverer Discoverer { get; }

}