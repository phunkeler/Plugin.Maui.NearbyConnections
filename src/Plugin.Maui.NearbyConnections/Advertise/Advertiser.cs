namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    bool _disposed;

    internal Advertiser(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal bool IsAdvertising { get; private set; }

    internal Task StartAdvertisingAsync(string displayName)
        => PlatformStartAdvertising(displayName);

    internal void StopAdvertising()
        => PlatformStopAdvertising();
}