namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event fired when a peer invitation is received.
/// Maps to Android's IConnectionLifecycleCallback.OnConnectionInitiated and
/// iOS's MCNearbyServiceAdvertiserDelegate.DidReceiveInvitationFromPeer.
/// </summary>
public class InvitationReceived(
    string eventId,
    DateTimeOffset timestamp,
    NearbyDevice from) : INearbyConnectionsEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public string EventId { get; } = eventId;

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// The <see cref="NearbyDevice"/> that sent the invitation to connect.
    /// </summary>
    public NearbyDevice From { get; } = from;
}