using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for registering Plugin.Maui.NearbyConnections services
/// with the MAUI dependency injection container.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="INearbyConnections"/> as a singleton to the MAUI app's service collection
    /// and optional configuration of <see cref="NearbyConnectionsOptions"/>.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to register the Plugin.Maui.NearbyConnections plugin with.</param>
    /// <param name="options">Optional options to configure the plugin. If not provided, defaults are used.</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(
        this MauiAppBuilder builder,
        NearbyConnectionsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<INearbyConnections>(_ =>
        {
            var events = new NearbyConnectionsEvents();
            var deviceManager = new NearbyDeviceManager(TimeProvider.System, events);

            return new NearbyConnectionsImplementation(deviceManager, TimeProvider.System, events, options ?? new());
        });

        return builder;
    }
}