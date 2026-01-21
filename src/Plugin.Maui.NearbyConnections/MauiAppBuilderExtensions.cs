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
    /// <param name="configureOptions">Optional action to configure plugin options</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        IAppInfo appInfo = AppInfo.Current;
        var options = new NearbyConnectionsOptions();
        configureOptions?.Invoke(options);
        NearbyConnections.Current.Options = options;

        builder.Services.AddSingleton(NearbyConnections.Current);
        return builder;
    }
}