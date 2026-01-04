namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    bool _disposed;

    internal Discoverer(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal bool IsDiscovering { get; private set; }

    internal Task StartDiscoveringAsync()
        => PlatformStartDiscovering();

    internal void StopDiscovering()
        => PlatformStopDiscovering();
}