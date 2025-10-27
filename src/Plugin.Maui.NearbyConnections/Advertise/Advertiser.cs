namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    internal Advertiser(NearbyConnectionsImplementation nearbyConnections)
    {
        _nearbyConnections = nearbyConnections;
    }

    internal Task StartAdvertisingAsync(AdvertiseOptions options)
        => PlatformStartAdvertising(options);

    internal void StopAdvertising()
        => PlatformStopAdvertising();
}