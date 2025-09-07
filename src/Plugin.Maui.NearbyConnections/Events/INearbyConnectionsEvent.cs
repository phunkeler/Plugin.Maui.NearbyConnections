namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Base interface for all nearby connections events.
/// </summary>
public interface INearbyConnectionsEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// When this event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }
}