using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using AdvertisingOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides access to the Nearby Connections plugin functionality.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    /// Fired when the advertising state changes.
    /// This event remains active across advertiser dispose/recreate cycles.
    /// </summary>
    event EventHandler<AdvertisingStateChangedEventArgs> AdvertisingStateChanged;

    /// <summary>
    /// Fired when the discovering state changes.
    /// This event remains active across discoverer dispose/recreate cycles.
    /// </summary>
    event EventHandler<DiscoveringStateChangedEventArgs> DiscoveringStateChanged;

    /// <summary>
    /// Starts advertising for nearby devices.
    /// </summary>
    /// <param name="options">
    /// The <see cref="AdvertisingOptions"/> to use for configuring advertising behavior.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising for nearby devices.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts discovering for nearby devices.
    /// </summary>
    /// <param name="options">
    /// The <see cref="DiscoveringOptions"/> to use for configuring discovery behavior.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StartDiscoveryAsync(DiscoveringOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovering for nearby devices.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);
}