using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser
{
    readonly NearbyConnectionsImplementation _nearbyConnections;
    readonly ILogger _logger;

    internal Advertiser(NearbyConnectionsImplementation nearbyConnections, ILoggerFactory loggerFactory)
    {
        _nearbyConnections = nearbyConnections;
        _logger = loggerFactory.CreateLogger<Advertiser>();
    }

    internal Task StartAdvertisingAsync(AdvertiseOptions options)
        => PlatformStartAdvertising(options);

    internal void StopAdvertising()
        => PlatformStopAdvertising();
}