using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Extension methods for registering Plugin.Maui.NearbyConnections services
/// with a <see cref="MauiAppBuilder"/>.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="INearbySession"/> as a singleton and configures
    /// <see cref="NearbyConnectionsOptions"/>.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to register with.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="NearbyConnectionsOptions"/>.
    /// When <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>The same <see cref="MauiAppBuilder"/> for chaining.</returns>
    /// <remarks>
    /// Nothing starts advertising or discovering on its own — both are explicit calls on the
    /// resolved <see cref="INearbySession"/>, so permission prompts happen when the app decides.
    /// </remarks>
    public static MauiAppBuilder UseNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddNearbyConnections(configure);

        return builder;
    }
}
