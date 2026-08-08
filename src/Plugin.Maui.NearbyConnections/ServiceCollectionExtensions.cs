using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides extension methods for adding nearby connectivity to an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INearbyConnections"/> and its configuration with the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configure">
    /// An optional delegate that configures <see cref="NearbyConnectionsOptions"/>. If
    /// <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance, so that multiple calls can be chained.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="INearbyConnections"/> is registered as a singleton, because the underlying resources
    /// are singular: one radio and one native session per device. Options are configured through
    /// the <see cref="IOptions{TOptions}"/> pipeline and validated at startup.
    /// </para>
    /// <para>
    /// Neither advertising nor discovery starts automatically. Call
    /// <see cref="INearbyConnections.StartAdvertisingAsync(CancellationToken)"/> or
    /// <see cref="INearbyConnections.StartDiscoveringAsync(CancellationToken)"/> once the application
    /// is ready and the required permissions have been granted.
    /// </para>
    /// <para>
    /// The session is constructed during application startup rather than on first resolution, so a
    /// service that subscribes to <see cref="INearbyConnections.ConnectionEstablished"/> at startup is
    /// guaranteed to be attached before any connection can be established. Constructing the session
    /// does not start advertising or discovery.
    /// </para>
    /// <para>
    /// The session resolves <see cref="IDispatcher"/> when one is registered, which is always the
    /// case in a .NET MAUI application, and uses it to marshal device state changes and events onto
    /// the UI thread. When no dispatcher is registered, as in unit tests, callbacks are raised on
    /// the thread the platform used.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
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

        services.TryAddSingleton<INearbyConnections>(sp =>
        {
            var resolvedOptions = sp.GetRequiredService<IOptions<NearbyConnectionsOptions>>().Value;
            var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            var logger = sp.GetService<ILogger<PlatformNearbyConnections>>()
                ?? NullLogger<PlatformNearbyConnections>.Instance;
#if IOS
            var remotePeers = new PeerRegistry<MCPeerID>();
            var peerKeyProvider = new PeerKeyProvider(
                sp.GetService<ILogger<PeerKeyProvider>>() ?? NullLogger<PeerKeyProvider>.Instance);
            var localPeerIdentityStore = new LocalPeerIdentityStore(
                sp.GetService<ILogger<LocalPeerIdentityStore>>() ?? NullLogger<LocalPeerIdentityStore>.Instance);
#endif
            var connections = new PlatformNearbyConnections(
                timeProvider,
                resolvedOptions,
                logger
#if IOS
                , remotePeers
                , peerKeyProvider
                , localPeerIdentityStore
#endif
            );

            return new NearbyConnectionsImplementation(
                connections,
                sp.GetService<IDispatcher>(),
                sp.GetService<ILogger<NearbyConnectionsImplementation>>() ?? NullLogger<NearbyConnectionsImplementation>.Instance);
        });

        // Forces the session into existence during MauiAppBuilder.Build(), rather than leaving it
        // to whichever consumer happens to resolve it first. See NearbyConnectionsInitializer for the
        // silent payload loss this prevents. TryAddEnumerable because MAUI runs these via
        // GetServices<T>(): a second AddNearbyConnections() call would otherwise register a
        // duplicate initializer.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMauiInitializeService, NearbyConnectionsInitializer>());

        return services;
    }

    /// <summary>
    /// Constructs <see cref="INearbyConnections"/> during <c>MauiAppBuilder.Build()</c> so the session
    /// — and any consumer that subscribes to it at startup — is alive before the first connection
    /// can be established.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="INearbyConnections.ConnectionEstablished"/> is a plain event with no replay. The
    /// container creates singletons lazily, on first resolution, so without this the session might
    /// not exist until a page injected it — and a connection established before that point raises
    /// an event nobody is subscribed to. Inbound payloads are then written to a channel with no
    /// reader: no exception, no log, messages simply never arrive.
    /// </para>
    /// <para>
    /// Resolving the session is the entire job; the resolved instance is deliberately discarded
    /// because the container owns it from here on.
    /// </para>
    /// </remarks>
    sealed class NearbyConnectionsInitializer : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
            => services.GetRequiredService<INearbyConnections>();
    }
}
