namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Base interface for all nearby connections events.
/// </summary>
public interface INearbyConnectionsEvent
{
    /// <summary>
    /// A unique identifier for the event.
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// The timestamp when the event was created.
    /// </summary>
    DateTimeOffset Created { get; }
}