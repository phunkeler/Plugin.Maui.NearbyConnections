using Microsoft.Extensions.DependencyInjection.Extensions;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for configuring the plugin.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app with default configuration.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(this MauiAppBuilder builder)
        => builder.AddNearbyConnections(_ => { });

    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app with configuration options.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <param name="configure">Configuration delegate for nearby connections</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(
        this MauiAppBuilder builder, 
        Action<NearbyConnectionsBuilder> configure)
    {
        var nearbyBuilder = new NearbyConnectionsBuilder(builder.Services);
        configure(nearbyBuilder);
        nearbyBuilder.Build();

        return builder;
    }
}

/// <summary>
/// Builder for configuring Nearby Connections plugin registration.
/// </summary>
public class NearbyConnectionsBuilder
{
    readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection</param>
    public NearbyConnectionsBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Configures a custom advertiser factory.
    /// </summary>
    /// <param name="factory">Custom advertiser factory</param>
    /// <returns>The builder for chaining</returns>
    public NearbyConnectionsBuilder WithAdvertiserFactory<TFactory>() 
        where TFactory : class, IAdvertiserFactory
    {
        _services.AddSingleton<IAdvertiserFactory, TFactory>();
        return this;
    }

    /// <summary>
    /// Configures a custom discoverer factory.
    /// </summary>
    /// <param name="factory">Custom discoverer factory</param>
    /// <returns>The builder for chaining</returns>
    public NearbyConnectionsBuilder WithDiscovererFactory<TFactory>() 
        where TFactory : class, IDiscovererFactory
    {
        _services.AddSingleton<IDiscovererFactory, TFactory>();
        return this;
    }

    /// <summary>
    /// Configures a custom implementation of the nearby connections service.
    /// </summary>
    /// <typeparam name="TImplementation">The implementation type</typeparam>
    /// <returns>The builder for chaining</returns>
    public NearbyConnectionsBuilder WithImplementation<TImplementation>()
        where TImplementation : class, INearbyConnections
    {
        _services.AddSingleton<INearbyConnections, TImplementation>();
        return this;
    }

    /// <summary>
    /// Builds and registers the Nearby Connections services.
    /// </summary>
    internal void Build()
    {
        // Register default factories if not already registered
        _services.TryAddSingleton<IAdvertiserFactory, AdvertiserFactory>();
        _services.TryAddSingleton<IDiscovererFactory, DiscovererFactory>();

        // Register the main implementation if not already registered
        _services.TryAddSingleton<INearbyConnections>(serviceProvider =>
        {
            var implementation = new NearbyConnectionsImplementation(
                serviceProvider.GetRequiredService<IAdvertiserFactory>(),
                serviceProvider.GetRequiredService<IDiscovererFactory>());

            // Set the static instance for consumers using the static pattern
            NearbyConnections.SetCurrent(implementation);

            return implementation;
        });
    }
}