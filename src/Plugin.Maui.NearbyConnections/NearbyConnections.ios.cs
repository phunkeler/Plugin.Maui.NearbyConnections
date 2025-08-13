using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    public Task StartDiscoveryAsync()
    {
        var advertiser = new MCNearbyServiceAdvertiser();
        return Task.CompletedTask;
    }
}