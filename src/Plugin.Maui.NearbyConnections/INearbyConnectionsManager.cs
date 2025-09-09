namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The primary interface for interacting with this API.
/// Currently, this is only useful for creating the session object which is the next-most important interface.
/// </summary>
public interface INearbyConnectionsManager : IDisposable
{
    /// <summary>
    /// Createa a new <see cref="INearbyConnectionsSession"/> with the
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<INearbyConnectionsSession> CreateSessionAsync(NearbyConnectionsSessionOptions? options = default);
}