namespace Plugin.Maui.NearbyConnections;

/// <summary>
///     Interface for Nearby Connections plugin.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    ///     Start discovering nearby peers
    /// </summary>
    Task StartDiscoveryAsync(DiscoveryOptions options);
}