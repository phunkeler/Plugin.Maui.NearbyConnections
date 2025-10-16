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
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the main plugin interface
        builder.Services.AddSingleton<INearbyConnections, NearbyConnectionsImplementation>();

        return builder;
    }
}