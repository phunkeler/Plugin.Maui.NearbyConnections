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
    /// Registers <see cref="INearbyConnections"/> (Tier 1) as a singleton and configures
    /// <see cref="NearbyConnectionsOptions"/> via the <see cref="IOptions{TOptions}"/> pipeline.
    /// Call <see cref="AddAdvertiser"/> and/or <see cref="AddDiscoverer"/> on the returned
    /// builder to opt in to Tier 2 services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="NearbyConnectionsOptions"/>.
    /// When <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>A <see cref="NearbyConnectionsBuilder"/> for registering optional Tier 2 services.</returns>
    public static NearbyConnectionsBuilder AddNearbyConnections(
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

        services.AddSingleton<INearbyConnections>(sp =>
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
            return new NearbyConnectionsImplementation(
                timeProvider,
                resolvedOptions,
                logger
#if IOS
                , remotePeers
                , peerKeyProvider
                , localPeerIdentityStore
#endif
            );
        });

        return new NearbyConnectionsBuilder(services);
    }

    /// <summary>
    /// Registers <see cref="INearbyAdvertiser"/> as a singleton.
    /// </summary>
    /// <param name="builder">The <see cref="NearbyConnectionsBuilder"/> to register with.</param>
    /// <returns>The same <see cref="NearbyConnectionsBuilder"/> for chaining.</returns>
    public static NearbyConnectionsBuilder AddAdvertiser(this NearbyConnectionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<INearbyAdvertiser>(sp =>
            new NearbyAdvertiser(
                sp.GetRequiredService<INearbyConnections>(),
                sp.GetService<ILogger<NearbyAdvertiser>>() ?? NullLogger<NearbyAdvertiser>.Instance));
        return builder;
    }

    /// <summary>
    /// Registers <see cref="INearbyDiscoverer"/> as a singleton.
    /// </summary>
    /// <param name="builder">The <see cref="NearbyConnectionsBuilder"/> to register with.</param>
    /// <returns>The same <see cref="NearbyConnectionsBuilder"/> for chaining.</returns>
    public static NearbyConnectionsBuilder AddDiscoverer(this NearbyConnectionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<INearbyDiscoverer>(sp =>
            new NearbyDiscoverer(
                sp.GetRequiredService<INearbyConnections>(),
                sp.GetService<ILogger<NearbyDiscoverer>>() ?? NullLogger<NearbyDiscoverer>.Instance));
        return builder;
    }
}
