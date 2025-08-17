using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for configuring the plugin.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <returns>
    /// The <see cref="MauiAppBuilder"/> for chaining
    /// </returns>
    public static MauiAppBuilder AddNearbyConnections(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IAdvertiserFactory, AdvertiserFactory>();
        builder.Services.AddSingleton<IDiscovererFactory, DiscovererFactory>();

        // Register a factory that also sets the static Current property
        builder.Services.AddSingleton<INearbyConnections>(serviceProvider =>
        {
            var implementation = new NearbyConnectionsImplementation(
                serviceProvider.GetRequiredService<IAdvertiserFactory>(),
                serviceProvider.GetRequiredService<IDiscovererFactory>());

            // Set the static instance for consumers using the static pattern
            NearbyConnections.SetCurrent(implementation);

            return implementation;
        });

        return builder;
    }
}