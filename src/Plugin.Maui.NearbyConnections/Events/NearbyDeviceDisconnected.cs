namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event that occurs when a <see cref="INearbyDevice" /> disconnects
/// or becomes unreachable.
/// </summary>
/// <remarks>
/// TODO: Distinguish between <see cref="NearbyDeviceLost"/> .
/// </remarks>
public class NearbyDeviceDisconnected(
    string eventId,
    DateTimeOffset timestamp,
    INearbyDevice device) : INearbyConnectionsEvent
{
    /// <inheritdoc/>
    public string EventId { get; } = eventId;

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// The <see cref="INearbyDevice"/> that disconnected.
    /// </summary>
    public INearbyDevice Device { get; } = device;
}