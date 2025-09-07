namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The primary interface for interacting with the Nearby Connections plugin.
/// </summary>
public interface INearbyConnectionsManager : IDisposable
{
    /// <summary>
    /// Createa a new <see cref="INearbyConnectionsSession"/>
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<INearbyConnectionsSession> CreateSessionAsync(NearbyConnectionsSessionOptions? options = default);
}