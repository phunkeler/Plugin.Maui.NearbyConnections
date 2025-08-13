using System;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for configuring the Nearby Connections plugin.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Adds the Nearby Connections plugin to the MAUI app.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configureOptions">Optional action to configure the Nearby Connections options.</param>
    /// <returns></returns>
    public static MauiAppBuilder AddNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsOptions>? configureOptions = null)
    {
        var options = new NearbyConnectionsOptions();
        configureOptions?.Invoke(options);

        builder.Services.AddSingleton(NearbyConnections.Current);
        return builder;
    }
}

/// <summary>
/// Options for configuring the Nearby Connections plugin.
/// </summary>
public partial class NearbyConnectionsOptions
{

}
