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

        builder.Services.AddSingleton<INearbyConnections>(sp =>
        {
            NearbyConnectionsImplementation? impl = null;

            var deviceManager = new NearbyDeviceManager(
                TimeProvider.System,
                (device, previousState, timeStamp) => impl!.OnDeviceStateChanged(device, previousState, timeStamp));

            var dispatcher = sp.GetRequiredService<IDispatcher>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Plugin.Maui.NearbyConnections.INearbyConnections");

            impl = new NearbyConnectionsImplementation(
                deviceManager,
                dispatcher,
                TimeProvider.System,
                options ?? new(),
                logger
#if IOS
                , new PeerIdManager(sp.GetRequiredService<ILogger<PeerIdManager>>())
#endif
            );

            return impl;
        });

        return builder;
    }
}