namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Base interface for all nearby connections events.
/// </summary>
public interface INearbyConnectionsEvent
{
    /// <summary>
    /// A unique identifier for the event.
    /// Should we use "InstallationId" here?
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// The timestamp when the event was created.
    /// </summary>
    DateTimeOffset Created { get; }
}