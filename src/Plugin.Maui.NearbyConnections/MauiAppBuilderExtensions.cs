using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using AdvertisingOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for configuring the plugin.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Configures the <see cref="MauiAppBuilder"/> with the Nearby Connections plugin commponents.
    /// This represents the default configuration.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder ConfigureNearbyConnections(this MauiAppBuilder builder)
        => builder.ConfigureNearbyConnections(_ => { });

    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app with configuration options.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <param name="configure">Configuration delegate for nearby connections</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder ConfigureNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsBuilder> configure)
    {
        // If any of our types were registered before HERE, they will be overwritten NOW
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
    /// Configures the NearbyConnections options.
    /// </summary>
    /// <param name="configure">Configuration delegate for nearby connections options</param>
    /// <returns>The builder for chaining</returns>
    public NearbyConnectionsBuilder Configure(Action<NearbyConnectionsOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Configures the advertising options.
    /// </summary>
    /// <param name="configure">Configuration delegate for advertising options</param>
    /// <returns>The builder for chaining</returns>
    public NearbyConnectionsBuilder ConfigureAdvertising(Action<AdvertisingOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Configures the discovery options.
    /// </summary>
    /// <param name="configure">Configuration delegate for discovery options</param>
    /// <returns>The builder for chaining</returns>
    public NearbyConnectionsBuilder ConfigureDiscovery(Action<DiscoveringOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Builds and registers the Nearby Connections services.
    /// </summary>
    internal void Build()
    {
        _services.TryAddSingleton<IAdvertiserFactory, AdvertiserFactory>();
        _services.TryAddSingleton<IDiscovererFactory, DiscovererFactory>();
        _services.TryAddSingleton<INearbyConnections, NearbyConnectionsImplementation>();
    }
}