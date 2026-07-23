using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Extension methods for registering Plugin.Maui.NearbyDevices services
/// with a <see cref="MauiAppBuilder"/>.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="INearbyDevices"/> (Tier 1) and configures
    /// <see cref="NearbyDevicesOptions"/>. Call
    /// <see cref="ServiceCollectionExtensions.AddAdvertiser"/> and/or
    /// <see cref="ServiceCollectionExtensions.AddDiscoverer"/> on the returned builder
    /// to opt in to Tier 2 services.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to register with.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="NearbyDevicesOptions"/>.
    /// When <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>A <see cref="NearbyDevicesBuilder"/> for registering optional services.</returns>
    public static NearbyDevicesBuilder UseNearbyDevices(
        this MauiAppBuilder builder,
        Action<NearbyDevicesOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddNearbyDevices(configure);
    }
}
