namespace Plugin.Maui.NearbyConnections.Events.Pipeline;

/// <summary>
/// Processes events through a pipeline.
/// </summary>
/// <typeparam name="TEvent">Event type being processed</typeparam>
public interface INearbyConnectionsEventProcessor<TEvent>
    where TEvent : INearbyConnectionsEvent
{
    /// <summary>
    /// Process a <see cref="INearbyConnectionsEvent"/>.
    /// </summary>
    /// <param name="nearbyConnectionsEvent">The event to process</param>
    /// <returns>Processed event, or null to filter it out</returns>
    TEvent? Process(TEvent nearbyConnectionsEvent);
}