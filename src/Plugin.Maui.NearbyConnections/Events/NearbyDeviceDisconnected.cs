namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event that occurs when a <see cref="NearbyDevice" /> disconnects
/// or becomes unreachable.
/// </summary>
/// <remarks>
/// TODO: Distinguish between <see cref="NearbyDeviceLost"/> .
/// </remarks>
public class NearbyDeviceDisconnected(
    string eventId,
    DateTimeOffset timestamp,
    NearbyDevice device) : INearbyConnectionsEvent
{
    /// <inheritdoc/>
    public string EventId { get; } = eventId;

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// The <see cref="NearbyDevice"/> that disconnected.
    /// </summary>
    public NearbyDevice Device { get; } = device;
}