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

/// <summary>
///     This class provides access to the Nearby Connections plugin functionality.
/// </summary>
public static class NearbyConnections
{
    static INearbyConnections? s_currentImplementation;

    /// <summary>
    ///     Provides the default implementation for static usage of this API.
    /// </summary>
    public static INearbyConnections Current =>
        s_currentImplementation ??= CreateDefaultImplementation();

    /// <summary>
    /// Sets the current implementation. This is typically called by the DI container.
    /// </summary>
    /// <param name="implementation">The implementation to use</param>
    public static void SetCurrent(INearbyConnections implementation)
    {
        s_currentImplementation = implementation;
    }

    static NearbyConnectionsImplementation CreateDefaultImplementation()
    {
        var advertiserFactory = new AdvertiserFactory();
        var discovererFactory = new DiscovererFactory();
        return new NearbyConnectionsImplementation(advertiserFactory, discovererFactory);
    }
}

public partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly IAdvertiserFactory _advertiserFactory;
    readonly IDiscovererFactory _discovererFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsImplementation"/> class.
    /// </summary>
    /// <param name="advertiserFactory">
    /// The factory to create <see cref="IAdvertiser"/> instances.
    /// </param>
    /// <param name="discovererFactory">
    /// The factory to create <see cref="IDiscoverer"/> instances.
    /// </param>
    public NearbyConnectionsImplementation(
        IAdvertiserFactory advertiserFactory,
        IDiscovererFactory discovererFactory)
    {
        ArgumentNullException.ThrowIfNull(advertiserFactory);
        ArgumentNullException.ThrowIfNull(discovererFactory);

        _advertiserFactory = advertiserFactory;
        _discovererFactory = discovererFactory;
    }
}

/// <summary>
/// Advertising state change event arguments
/// </summary>
public class AdvertisingStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Indicates whether advertising is currently active.
    /// </summary>
    public bool IsAdvertising { get; init; }

    /// <summary>
    /// Gets the timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for discovering state changes.
/// </summary>
public class DiscoveringStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Indicates whether discovering is currently active.
    /// </summary>
    public bool IsDiscovering { get; init; }

    /// <summary>
    /// Gets the timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
