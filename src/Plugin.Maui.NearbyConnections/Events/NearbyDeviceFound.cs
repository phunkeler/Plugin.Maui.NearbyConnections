namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event representing a found nearby connection.
/// </summary>
public class NearbyDeviceFound(
    string eventId,
    DateTimeOffset timestamp,
    NearbyDevice device) : INearbyConnectionsEvent
{
    /// <inheritdoc/>
    public string EventId { get; } = eventId;

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// The <see cref="NearbyDevice" /> that was discovered.
    /// </summary>
    public NearbyDevice Device { get; } = device;
}