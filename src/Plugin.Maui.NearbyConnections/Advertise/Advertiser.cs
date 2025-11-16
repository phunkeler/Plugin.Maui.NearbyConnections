namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser : IDisposable
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    internal Advertiser(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal Task StartAdvertisingAsync(AdvertisingOptions options)
        => PlatformStartAdvertising(options);

    internal void StopAdvertising()
        => PlatformStopAdvertising();
}