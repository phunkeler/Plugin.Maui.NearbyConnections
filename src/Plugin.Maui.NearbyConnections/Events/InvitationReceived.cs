using Plugin.Maui.NearbyConnections.Models;

namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event fired when a peer invitation is received.
/// Maps to Android's IConnectionLifecycleCallback.OnConnectionInitiated and
/// iOS's MCNearbyServiceAdvertiserDelegate.DidReceiveInvitationFromPeer.
/// </summary>
public class InvitationReceived : INearbyConnectionsEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The peer device that sent the invitation.
    /// </summary>
    public required PeerDevice InvitingPeer { get; init; }

    /// <summary>
    /// Optional authentication token or connection info from the inviting peer.
    /// On Android: ConnectionInfo.AuthenticationDigits
    /// On iOS: context data from the invitation
    /// </summary>
    public string? AuthenticationToken { get; init; }

    /// <summary>
    /// Optional context data sent with the invitation.
    /// </summary>
    public string? InvitationContext { get; init; }

    /// <summary>
    /// Indicates if this invitation requires user acceptance.
    /// True for manual connections, false for automatic ones.
    /// </summary>
    public bool RequiresAcceptance { get; init; } = true;

    /// <summary>
    /// Platform-specific connection endpoint identifier.
    /// On Android: endpointId from OnConnectionInitiated
    /// On iOS: MCPeerID.DisplayName
    /// </summary>
    public required string ConnectionEndpoint { get; init; }

    /// <summary>
    /// The timestamp when the event was created.
    /// </summary>
    public DateTimeOffset Created => throw new NotImplementedException();
}