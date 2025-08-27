namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Base interface for all nearby connections events.
/// </summary>
public interface INearbyConnectionsEvent
{
    string EventId { get; }
    DateTimeOffset Timestamp { get; }
}