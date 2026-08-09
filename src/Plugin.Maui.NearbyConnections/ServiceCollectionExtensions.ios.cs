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
        var remotePeers = new PeerRegistry<MCPeerID>();
        var peerKeyProvider = new PeerKeyProvider(
            services.GetService<ILogger<PeerKeyProvider>>() ?? NullLogger<PeerKeyProvider>.Instance);
        var localPeerIdentityStore = new LocalPeerIdentityStore(
            services.GetService<ILogger<LocalPeerIdentityStore>>() ?? NullLogger<LocalPeerIdentityStore>.Instance);

        return new PlatformNearby(
            timeProvider,
            options,
            logger,
            remotePeers,
            peerKeyProvider,
            localPeerIdentityStore);
    }
}
