using Android.Gms.Nearby.Connection;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    public Task StartAdvertisingAsync() => throw new NotImplementedException();

    public Task StartDiscoveryAsync()
    {
        return Task.CompletedTask;
    }

}