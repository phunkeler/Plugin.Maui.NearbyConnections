namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer : IDisposable
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    internal Discoverer(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal Task StartDiscoveringAsync()
        => PlatformStartDiscovering();

    internal void StopDiscovering()
        => PlatformStopDiscovering();
}