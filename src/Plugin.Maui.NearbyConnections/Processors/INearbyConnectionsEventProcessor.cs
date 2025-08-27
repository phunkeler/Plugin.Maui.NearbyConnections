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
