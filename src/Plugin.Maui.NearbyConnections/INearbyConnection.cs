namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents an active nearby connection that handles advertising, discovery, and peer connections.
/// </summary>
public interface INearbyConnection : IDisposable
{
    /// <summary>
    /// Gets a unique identifier for this connection session.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the options used to create this connection.
    /// TODOL Why would we want to expose this?
    /// </summary>
    NearbyConnectionOptions Options { get; }

    /// <summary>
    /// Whether this connection session is currently active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// When this connection session was started.
    /// </summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Duration this session has been active.
    /// </summary>
    TimeSpan Duration { get; }

    // Connection management
    Task<ConnectionAttempt> BeginConnectionAsync(string peerId, CancellationToken cancellationToken = default);
    Task CancelConnectionAsync(string peerId, CancellationToken cancellationToken = default);
    Task DisconnectFromPeerAsync(string peerId, CancellationToken cancellationToken = default);

    // Invitation handling
    Task AcceptInvitationAsync(string connectionEndpoint, CancellationToken cancellationToken = default);
    Task RejectInvitationAsync(string connectionEndpoint, CancellationToken cancellationToken = default);

    // State queries
    IReadOnlyList<PeerDevice> GetDiscoveredPeers();
    IReadOnlyList<PeerDevice> GetConnectedPeers();
    IReadOnlyList<ConnectionAttempt> GetActiveConnectionAttempts();

    // Messaging
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
    Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default);

    // Events
    event EventHandler<PeerDiscoveredEventArgs> PeerDiscovered;
    event EventHandler<InvitationReceivedEventArgs> InvitationReceived;
    event EventHandler<ConnectionProgressEventArgs> ConnectionProgress;
    event EventHandler<ConnectionEstablishedEventArgs> ConnectionEstablished;
    event EventHandler<ConnectionFailedEventArgs> ConnectionFailed;
    event EventHandler<PeerMessageReceivedEventArgs> MessageReceived;

    // Session control
    Task StopAsync(CancellationToken cancellationToken = default);
}