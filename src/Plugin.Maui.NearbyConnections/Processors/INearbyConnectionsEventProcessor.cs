using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Processors;

/// <summary>
/// Processes nearby connections events.
/// </summary>
public interface INearbyConnectionsEventProcessor
{
    /// <summary>
    /// Processes the <see cref="INearbyConnectionsEvent"/>.
    /// </summary>
    /// <param name="nearbyConnectionsEvent"></param>
    /// <returns></returns>
    public INearbyConnectionsEvent? ProcessEvent(INearbyConnectionsEvent nearbyConnectionsEvent);
}

/// <summary>
/// The event processor.
/// </summary>
public class NearbyConnectionsEventProcessor : INearbyConnectionsEventProcessor
{
    /// <summary>
    /// Processor a nearby connections event prior to publishing.
    /// This happens immediately following transformation of the native
    /// event info.
    /// </summary>
    /// <param name="nearbyConnectionsEvent"></param>
    /// <returns>The processed event, or null to drop it.</returns>
    /// <exception cref="NotImplementedException"></exception>
    public INearbyConnectionsEvent? ProcessEvent(INearbyConnectionsEvent nearbyConnectionsEvent) => throw new NotImplementedException();
}
