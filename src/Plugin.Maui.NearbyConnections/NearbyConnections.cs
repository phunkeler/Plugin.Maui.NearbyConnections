using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// This class provides access to the Nearby Connections plugin functionality.
/// </summary>
public static class NearbyConnections
{
    static INearbyConnections? s_currentImplementation;

    /// <summary>
    ///     Provides the default implementation for static usage of this API.
    /// </summary>
    public static INearbyConnections Current =>
        s_currentImplementation ??= CreateDefaultImplementation();

    /// <summary>
    /// Sets the current implementation. This is typically called by the DI container.
    /// </summary>
    /// <param name="implementation">The implementation to use</param>
    public static void SetCurrent(INearbyConnections implementation)
    {
        s_currentImplementation = implementation;
    }

    static NearbyConnectionsImplementation CreateDefaultImplementation()
    {
        var advertiserFactory = new AdvertiserFactory();
        var discovererFactory = new DiscovererFactory();
        return new NearbyConnectionsImplementation(advertiserFactory, discovererFactory);
    }
}