namespace Plugin.Maui.NearbyConnections;

/// <summary>
///     Interface for Nearby Connections plugin.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    ///     Start discovering nearby peers
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