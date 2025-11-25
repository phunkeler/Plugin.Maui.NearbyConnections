namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser : IDisposable
{
    readonly NearbyConnections _nearbyConnections;

    internal Advertiser(NearbyConnections nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal Task StartAdvertisingAsync(string displayName)
        => PlatformStartAdvertising(displayName);

    internal void StopAdvertising()
        => PlatformStopAdvertising();
}