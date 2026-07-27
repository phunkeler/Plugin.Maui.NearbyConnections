using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for registering Plugin.Maui.NearbyConnections services
/// with a <see cref="MauiAppBuilder"/>.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="INearbyConnections"/> (Tier 1) and configures
    /// <see cref="NearbyConnectionsOptions"/>. Call
    /// <see cref="ServiceCollectionExtensions.AddAdvertiser"/> and/or
    /// <see cref="ServiceCollectionExtensions.AddDiscoverer"/> on the returned builder
    /// to opt in to Tier 2 services.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to register with.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="NearbyConnectionsOptions"/>.
    /// When <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>A <see cref="NearbyConnectionsBuilder"/> for registering optional services.</returns>
    public static NearbyConnectionsBuilder UseNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddNearbyConnections(configure);
    }
}
