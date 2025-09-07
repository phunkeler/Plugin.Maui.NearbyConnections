namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event representing a found nearby connection.
/// </summary>
/// <remarks>
/// TODO: Distinguish between <see cref="NearbyDeviceDisconnected"/> .
/// </remarks>
public class NearbyDeviceLost(
    string eventId,
    DateTimeOffset timestamp,
    INearbyDevice device) : INearbyConnectionsEvent
{
    /// <inheritdoc/>
    public string EventId { get; } = eventId;

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// The <see cref="INearbyDevice"/> that was lost.
    /// </summary>
    public INearbyDevice Device { get; } = device;
}
