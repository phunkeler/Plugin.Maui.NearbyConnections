namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The main interface for interacting with the Nearby Connections plugin.
/// </summary>
public interface INearbyConnections
{
    // High-level session management (orchestrator handles advertising/discovery internally)
    Task<INearbyConnection> StartAsync(NearbyConnectionOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    // Connection management
    Task<INearbyConnection> BeginConnectionAsync(string peerId, CancellationToken cancellationToken = default);
    Task CancelConnectionAsync(string peerId, CancellationToken cancellationToken = default);

    // Advertising/Discovery are NOT exposed - handled via options

}