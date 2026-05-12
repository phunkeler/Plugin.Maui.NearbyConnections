namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Discriminated union of all events that can be emitted by <see cref="INearbyAdvertiser"/>.
/// </summary>
public abstract record AdvertiserEvent
{
    /// <summary>
    /// An inbound connection request arrived and is awaiting accept or reject.
    /// </summary>
    /// <param name="Request">The pending connection request.</param>
    public sealed record ConnectionRequested(NearbyConnectionRequest Request) : AdvertiserEvent;

    /// <summary>
    /// A pending request was accepted and is now an active connection.
    /// </summary>
    /// <param name="Connection">The accepted connection.</param>
    public sealed record ConnectionAccepted(NearbyConnection Connection) : AdvertiserEvent;

    /// <summary>
    /// An active connection terminated from either side.
    /// </summary>
    /// <param name="Connection">The connection that was dropped.</param>
    public sealed record ConnectionDropped(NearbyConnection Connection) : AdvertiserEvent;

    /// <summary>
    /// A payload was received from an active connection.
    /// </summary>
    /// <param name="Connection">The connection that received the payload.</param>
    /// <param name="Payload">The payload that was received.</param>
    public sealed record PayloadReceived(NearbyConnection Connection, NearbyPayload Payload) : AdvertiserEvent;

    /// <summary>
    /// Emitted once after all current-state events have been replayed.
    /// Every event before this is synthetic; every event after this is live.
    /// </summary>
    public sealed record Synchronized : AdvertiserEvent;
}