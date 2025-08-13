namespace Plugin.Maui.NearbyConnections;

/// <summary>
///     Interface for Nearby Connections plugin.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    ///     0. Configure advertising behavior
    ///         0.1.
    ///     1. Create Advertiser
    ///     2. Start Advertising
    /// </summary>
    /// <returns></returns>
    Task StartAdvertisingAsync();

    /// <summary>
    ///     0. Configure
    ///     1. Create Advertiser
    ///     2. Start Advertising
    /// </summary>
    Task StartDiscoveryAsync();
}

/// <summary>
///     This class provides access to the Nearby Connections plugin functionality.
///     TODO: Determine if this is really a benefit or does more harm than good to consumers.
/// </summary>
public static class NearbyConnections
{
    static INearbyConnections? s_currentImplementation;

    /// <summary>
    ///     Provides the default implementation for static usage of this API.
    /// </summary>
    public static INearbyConnections Current =>
        s_currentImplementation ??= new NearbyConnectionsImplementation();
}

/// <summary>
/// Options for configuring advertising behavior.
/// </summary>
public interface IAdvertisingOptions
{
    /// <summary>
    /// Gets or sets the name of the service to advertise.
    /// </summary>
    string ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the discovery info to include in the advertisement.
    /// </summary>
    IDictionary<string, string> DiscoveryInfo { get; set; }
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