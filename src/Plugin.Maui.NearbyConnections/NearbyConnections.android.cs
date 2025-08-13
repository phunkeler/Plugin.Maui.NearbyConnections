using Android.Gms.Nearby.Connection;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    public Task StartDiscoveryAsync()
    {
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity);
        return Task.CompletedTask;
    }

}