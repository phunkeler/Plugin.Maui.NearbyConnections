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
    /// <param name="configureOptions">Optional action to configure plugin options</param>
    /// <returns>The <see cref="MauiAppBuilder"/> for chaining</returns>
    public static MauiAppBuilder AddNearbyConnections(
        this MauiAppBuilder builder,
        Action<NearbyConnectionsOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configureOptions is not null)
        {
            builder.Services.Configure(configureOptions);
        }

        builder.Services.TryAddSingleton<INearbyConnections>(sp =>
        {
            var options = sp.GetService<IOptions<NearbyConnectionsOptions>>()?.Value
                ?? new NearbyConnectionsOptions();

            var loggerFactory = sp.GetService<ILoggerFactory>()
                ?? NullLoggerFactory.Instance;

            var timeProvider = sp.GetService<TimeProvider>()
                ?? TimeProvider.System;

            return new NearbyConnections(
                options,
                loggerFactory,
                timeProvider);
        });

        return builder;
    }
}