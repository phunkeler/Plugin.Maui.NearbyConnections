using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Extension methods for registering Plugin.Maui.NearbyDevices services
/// with a dependency injection service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INearbyDevices"/> (Tier 1) as a singleton and configures
    /// <see cref="NearbyDevicesOptions"/> via the <see cref="IOptions{TOptions}"/> pipeline.
    /// Call <see cref="AddAdvertiser"/> and/or <see cref="AddDiscoverer"/> on the returned
    /// builder to opt in to Tier 2 services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="NearbyDevicesOptions"/>.
    /// When <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>A <see cref="NearbyDevicesBuilder"/> for registering optional Tier 2 services.</returns>
    public static NearbyDevicesBuilder AddNearbyDevices(
        this IServiceCollection services,
        Action<NearbyDevicesOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddOptions<NearbyDevicesOptions>().ValidateOnStart();
        services.AddSingleton<IConfigureOptions<NearbyDevicesOptions>, NearbyDevicesOptionsSetup>();
        services.AddSingleton<IValidateOptions<NearbyDevicesOptions>, NearbyDevicesOptionsValidator>();

        services.AddSingleton<INearbyDevices>(sp =>
        {
            var resolvedOptions = sp.GetRequiredService<IOptions<NearbyDevicesOptions>>().Value;
            var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            var deviceManager = new NearbyDeviceManager();
            var logger = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger<NearbyDevicesImplementation>();
            return new NearbyDevicesImplementation(
                deviceManager,
                timeProvider,
                resolvedOptions,
                logger
#if IOS
                , new PeerIdManager(sp.GetRequiredService<ILogger<PeerIdManager>>())
#endif
            );
        });

        return new NearbyDevicesBuilder(services);
    }

    /// <summary>
    /// Registers <see cref="INearbyAdvertiser"/> as a singleton.
    /// </summary>
    /// <param name="builder">The <see cref="NearbyDevicesBuilder"/> to register with.</param>
    /// <returns>The same <see cref="NearbyDevicesBuilder"/> for chaining.</returns>
    public static NearbyDevicesBuilder AddAdvertiser(this NearbyDevicesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<INearbyAdvertiser>(sp =>
            new NearbyAdvertiser(
                sp.GetRequiredService<INearbyDevices>(),
                sp.GetRequiredService<ILogger<NearbyAdvertiser>>()));
        return builder;
    }

    /// <summary>
    /// Registers <see cref="INearbyDiscoverer"/> as a singleton.
    /// </summary>
    /// <param name="builder">The <see cref="NearbyDevicesBuilder"/> to register with.</param>
    /// <returns>The same <see cref="NearbyDevicesBuilder"/> for chaining.</returns>
    public static NearbyDevicesBuilder AddDiscoverer(this NearbyDevicesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<INearbyDiscoverer>(sp =>
            new NearbyDiscoverer(
                sp.GetRequiredService<INearbyDevices>(),
                sp.GetRequiredService<ILogger<NearbyDiscoverer>>()));
        return builder;
    }
}
