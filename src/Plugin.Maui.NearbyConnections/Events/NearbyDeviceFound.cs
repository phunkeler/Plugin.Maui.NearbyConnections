namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event representing a found nearby connection.
/// </summary>
public class NearbyDeviceFound(
    string eventId,
    DateTimeOffset timestamp,
    INearbyDevice device) : INearbyConnectionsEvent
{
    /// <inheritdoc/>
    public string EventId { get; } = eventId;

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// The <see cref="INearbyDevice" /> that was discovered.
    /// </summary>
    public INearbyDevice Device { get; } = device;
}