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
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to register the Plugin.Maui.NearbyConnections plugin with.</param>
    /// <param name="configureOptions">Optional action to configure plugin options</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining</returns>
    public static IServiceCollection AddNearbyConnections(
        this IServiceCollection serviceCollection,
        Action<NearbyConnectionsOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<NearbyConnectionsEvents>();
        serviceCollection.AddSingleton<INearbyDeviceManager, NearbyDeviceManager>();
        serviceCollection.AddSingleton<INearbyConnections>(sp =>
        {
            var deviceManager = sp.GetRequiredService<INearbyDeviceManager>();
            var events = sp.GetRequiredService<NearbyConnectionsEvents>();
            var instance = new NearbyConnectionsImplementation(deviceManager, events);

            var options = new NearbyConnectionsOptions();
            configureOptions?.Invoke(options);
            instance.Options = options;

            return instance;
        });

        return serviceCollection;
    }
}