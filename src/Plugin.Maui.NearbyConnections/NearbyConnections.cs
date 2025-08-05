namespace Plugin.Maui.NearbyConnections;

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
        s_currentImplementation ??= new NearbyConnectionsImplementation();
}