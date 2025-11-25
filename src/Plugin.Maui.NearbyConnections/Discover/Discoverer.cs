namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer : IDisposable
{
    readonly NearbyConnections _nearbyConnections;

    internal Discoverer(NearbyConnections nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal Task StartDiscoveringAsync()
        => PlatformStartDiscovering();

    internal void StopDiscovering()
        => PlatformStopDiscovering();
}