using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer
{
    readonly NearbyConnectionsImplementation _nearbyConnections;
    readonly ILogger _logger;

    internal Discoverer(NearbyConnectionsImplementation nearbyConnections, ILoggerFactory loggerFactory)
    {
        _nearbyConnections = nearbyConnections;
        _logger = loggerFactory.CreateLogger<Discoverer>();
    }

    internal Task StartDiscoveringAsync(DiscoverOptions options)
        => PlatformStartDiscovering(options);

    internal void StopDiscovering()
        => PlatformStopDiscovering();
}