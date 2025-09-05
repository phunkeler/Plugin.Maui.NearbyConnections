namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The main interface for interacting with the Nearby Connections plugin.
/// </summary>
public interface INearbyConnectionsManager : IDisposable
{
    /// <summary>
    /// Createa a new
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<INearbyConnectionsSession> CreateSessionAsync(NearbyConnectionsSessionOptions? options = default);
}