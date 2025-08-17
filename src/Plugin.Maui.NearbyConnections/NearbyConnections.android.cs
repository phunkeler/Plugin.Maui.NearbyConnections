using Plugin.Maui.NearbyConnections.Discover;
using AdvertisingOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions;

namespace Plugin.Maui.NearbyConnections;

public partial class NearbyConnectionsImplementation : INearbyConnections
{
    public Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StartDiscoveryAsync(DiscoveringOptions options, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}