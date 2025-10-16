namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Provides operations for discovering nearby advertising devices.
/// </summary>
public partial class Discoverer : IDiscoverer
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    internal Discoverer(NearbyConnectionsImplementation nearbyConnections)
    {
        _nearbyConnections = nearbyConnections;
    }

    public Task StartDiscoveringAsync(DiscoverOptions options)
        => PlatformStartDiscovering(options);

    public void StopDiscovering()
        => PlatformStopDiscovering();
}