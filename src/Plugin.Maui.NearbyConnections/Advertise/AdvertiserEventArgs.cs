namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Event arguments for when a connection is initiated from a remote peer.
/// </summary>
public class ConnectionInitiatedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the unique identifier of the remote endpoint.
    /// </summary>
    public required string EndpointId { get; init; }

    /// <summary>
    /// Gets the display name of the remote endpoint.
    /// </summary>
    public required string EndpointName { get; init; }
}

/// <summary>
/// Event arguments for when a connection result is received.
/// </summary>
public class ConnectionResultEventArgs : EventArgs
{
    /// <summary>
    /// Gets the unique identifier of the remote endpoint.
    /// </summary>
    public required string EndpointId { get; init; }

    /// <summary>
    /// Gets whether the connection was successful.
    /// </summary>
    public required bool Success { get; init; }
}

/// <summary>
/// Event arguments for when a peer disconnects.
/// </summary>
public class DisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the unique identifier of the disconnected endpoint.
    /// </summary>
    public required string EndpointId { get; init; }
}

/// <summary>
/// Event arguments for when an invitation is received (iOS only).
/// </summary>
public class InvitationReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the unique identifier of the peer sending the invitation.
    /// </summary>
    public required string PeerId { get; init; }

    /// <summary>
    /// Gets the display name of the peer sending the invitation.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets whether the invitation should be accepted.
    /// Set this to true to accept the invitation, false to reject it.
    /// </summary>
    public bool ShouldAccept { get; set; }
}