namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for registering Plugin.Maui.NearbyConnections services
/// with a dependency injection service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="INearbyConnections"/>, <see cref="INearbyAdvertiser"/>, and
    /// <see cref="INearbyDiscoverer"/> as singletons and configures
    /// <see cref="NearbyConnectionsOptions"/> via the <see cref="IOptions{TOptions}"/> pipeline.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="NearbyConnectionsOptions"/>.
    /// When <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
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
        services.AddSingleton<IConfigureOptions<NearbyConnectionsOptions>, NearbyConnectionsOptionsSetup>();
        services.AddSingleton<IValidateOptions<NearbyConnectionsOptions>, NearbyConnectionsOptionsValidator>();

        services.AddSingleton<INearbyConnections>(sp =>
        {
            var resolvedOptions = sp.GetRequiredService<IOptions<NearbyConnectionsOptions>>().Value;
            var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            var deviceManager = new NearbyDeviceManager(timeProvider);
            var logger = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger<NearbyConnectionsImplementation>();
            return new NearbyConnectionsImplementation(
                deviceManager,
                timeProvider,
                resolvedOptions,
                logger
#if IOS
                , new PeerIdManager(sp.GetRequiredService<ILogger<PeerIdManager>>())
#endif
            );
        });

        services.AddSingleton<INearbyAdvertiser>(sp =>
            new NearbyAdvertiser(
                sp.GetRequiredService<INearbyConnections>(),
                sp.GetRequiredService<IDispatcher>(),
                sp.GetRequiredService<ILogger<NearbyAdvertiser>>()));

        services.AddSingleton<INearbyDiscoverer>(sp =>
            new NearbyDiscoverer(
                sp.GetRequiredService<INearbyConnections>(),
                sp.GetRequiredService<IDispatcher>(),
                sp.GetRequiredService<ILogger<NearbyDiscoverer>>()));

        return services;
    }
}
