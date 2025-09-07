namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for configuring the plugin.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app with configuration options.
    /// </summary>
    /// <param name="builder">The MAUI app builder</param>
    /// <param name="configureOptions">Configuration delegate for nearby connections</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder ConfigureNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsOptions>? configureOptions = null)
    {
        NearbyConnectionsOptions? options = null;

        if (configureOptions is null)
        {
            options = new();
        }

        return builder;
    }
}