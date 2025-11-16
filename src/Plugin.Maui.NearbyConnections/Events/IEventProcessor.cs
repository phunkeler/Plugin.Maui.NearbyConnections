namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Processes events in the internal pipeline before they are exposed externally.
/// </summary>
public interface IEventProcessor
{
    /// <summary>
    /// Process an event, optionally transforming or filtering it.
    /// </summary>
    /// <param name="evt">The event to process</param>
    /// <returns>The processed event, or null to filter it out</returns>
    INearbyConnectionsEvent? Process(INearbyConnectionsEvent @evt);
}
