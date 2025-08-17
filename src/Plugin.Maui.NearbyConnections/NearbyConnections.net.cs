using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    public Task StartAdvertisingAsync(Advertise.AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StartDiscoveryAsync(DiscoveringOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}