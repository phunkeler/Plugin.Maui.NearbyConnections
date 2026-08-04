using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for registering Plugin.Maui.NearbyConnections services
/// with a dependency injection service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INearbySession"/> as a singleton and configures
    /// <see cref="NearbyConnectionsOptions"/> via the <see cref="IOptions{TOptions}"/> pipeline.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="NearbyConnectionsOptions"/>.
    /// When <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// A singleton because the resources underneath are singular: one radio, one native session.
    /// Nothing starts automatically — call <see cref="INearbySession.StartAdvertisingAsync"/> or
    /// <see cref="INearbySession.StartDiscoveringAsync"/> when the app is ready and permissions
    /// have been granted.
    /// </para>
    /// <para>
    /// The session resolves <see cref="IDispatcher"/> when one is registered (always, in a MAUI
    /// app) and uses it to marshal device-collection mutations and events to the UI thread. Without
    /// one — unit tests, or the plain <c>net10.0</c> target — callbacks run on the thread the
    /// platform raised them on.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddNearbyConnections(
        this IServiceCollection services,
        Action<NearbyConnectionsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddOptions<NearbyConnectionsOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<NearbyConnectionsOptions>, NearbyConnectionsOptionsValidator>();

        services.TryAddSingleton<INearbySession>(sp =>
        {
            var resolvedOptions = sp.GetRequiredService<IOptions<NearbyConnectionsOptions>>().Value;
            var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            var logger = sp.GetService<ILogger<NearbyConnectionsImplementation>>()
                ?? NullLogger<NearbyConnectionsImplementation>.Instance;
#if IOS
            var remotePeers = new PeerRegistry<MCPeerID>();
            var peerKeyProvider = new PeerKeyProvider(
                sp.GetService<ILogger<PeerKeyProvider>>() ?? NullLogger<PeerKeyProvider>.Instance);
            var localPeerIdentityStore = new LocalPeerIdentityStore(
                sp.GetService<ILogger<LocalPeerIdentityStore>>() ?? NullLogger<LocalPeerIdentityStore>.Instance);
#endif
            var connections = new NearbyConnectionsImplementation(
                timeProvider,
                resolvedOptions,
                logger
#if IOS
                , remotePeers
                , peerKeyProvider
                , localPeerIdentityStore
#endif
            );

            return new NearbySession(
                connections,
                sp.GetService<IDispatcher>(),
                sp.GetService<ILogger<NearbySession>>() ?? NullLogger<NearbySession>.Instance);
        });

        return services;
    }
}
