using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections;

public static partial class ServiceCollectionExtensions
{
    private static partial PlatformNearby CreatePlatformNearby(
        IServiceProvider services,
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger)
    {
        var peerKeyProvider = new PeerKeyProvider(
            services.GetService<ILogger<PeerKeyProvider>>() ?? NullLogger<PeerKeyProvider>.Instance);
        var peers = new PeerRegistry { PeerKeyProvider = peerKeyProvider, Logger = logger };
        var localPeerIdentityStore = new LocalPeerIdentityStore(
            services.GetService<ILogger<LocalPeerIdentityStore>>() ?? NullLogger<LocalPeerIdentityStore>.Instance);

        return new PlatformNearby(timeProvider, options, logger, peers)
        {
            PeerKeyProvider = peerKeyProvider,
            LocalPeerIdentityStore = localPeerIdentityStore,
        };
    }
}
