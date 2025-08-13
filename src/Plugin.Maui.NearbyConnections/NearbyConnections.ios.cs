using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    public Task StartDiscoveryAsync()
    {
        return Task.CompletedTask;
    }
}