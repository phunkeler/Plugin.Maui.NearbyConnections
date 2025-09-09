using Microsoft.Extensions.Options;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Events.Pipeline;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for configuring the plugin.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app with configuration from appsettings.json.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(this MauiAppBuilder builder)
    {
        return builder.AddNearbyConnections(_ => { });
    }

    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app with configuration options.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <param name="configureOptions">Configuration delegate for nearby connections</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var services = builder.Services;

        // Configure options with validation
        services.Configure(configureOptions);
        services.AddSingleton<IValidateOptions<NearbyConnectionsOptions>, NearbyConnectionsOptionsValidator>();

        // Register core services
        RegisterCoreServices(services);

        return builder;
    }


    private static void RegisterCoreServices(IServiceCollection services)
    {
        // Event system
        services.AddSingleton<INearbyConnectionsEventPipeline<INearbyConnectionsEvent>, NearbyConnectionsEventPipeline<INearbyConnectionsEvent>>();
        services.AddSingleton<INearbyConnectionsEventPublisher, NearbyConnectionsEventPublisher>();

        // Factories
        services.AddSingleton<IAdvertiserFactory, AdvertiserFactory>();
        services.AddSingleton<IDiscovererFactory, DiscovererFactory>();

        // Main plugin interface - using the manager pattern that exists
        services.AddSingleton<INearbyConnectionsManager, NearbyConnectionsManager>();
    }
}