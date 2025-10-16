using System.Threading;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for this device.
/// </summary>
public partial class Advertiser : IAdvertiser
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    internal Advertiser(NearbyConnectionsImplementation nearbyConnections)
    {
        _nearbyConnections = nearbyConnections;
    }

    public async Task StartAdvertisingAsync(AdvertiseOptions options)
        => await PlatformStartAdvertising(options);


    public void StopAdvertising()
        => PlatformStopAdvertising();
}