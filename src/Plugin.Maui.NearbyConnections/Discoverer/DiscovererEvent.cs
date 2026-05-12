namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Discriminated union of all events that can be emitted by <see cref="INearbyDiscoverer"/>.
/// </summary>
public abstract record DiscovererEvent
{
    /// <summary>
    /// A nearby device became visible during discovery.
    /// </summary>
    /// <param name="Device">The device that was found.</param>
    public sealed record DeviceFound(NearbyDevice Device) : DiscovererEvent;

    /// <summary>
    /// A previously visible device is no longer reachable.
    /// </summary>
    /// <param name="Device">The device that was lost.</param>
    public sealed record DeviceLost(NearbyDevice Device) : DiscovererEvent;

    /// <summary>
    /// A connection to a nearby device was successfully established.
    /// </summary>
    /// <param name="Connection">The newly established connection.</param>
    public sealed record DeviceConnected(NearbyConnection Connection) : DiscovererEvent;

    /// <summary>
    /// An active connection terminated from either side.
    /// </summary>
    /// <param name="Connection">The connection that was dropped.</param>
    public sealed record DeviceDisconnected(NearbyConnection Connection) : DiscovererEvent;

    /// <summary>
    /// A payload was received from an active connection.
    /// </summary>
    /// <param name="Connection">The connection that received the payload.</param>
    /// <param name="Payload">The payload that was received.</param>
    public sealed record PayloadReceived(NearbyConnection Connection, NearbyPayload Payload) : DiscovererEvent;

    /// <summary>
    /// Emitted once after all current-state events have been replayed.
    /// Every event before this is synthetic; every event after this is live.
    /// </summary>
    public sealed record Synchronized : DiscovererEvent;
}
