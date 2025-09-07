namespace Plugin.Maui.NearbyConnections.Events.Adapters;

/// <summary>
/// Transforms platform-specific arguments into cross-platform events.
/// </summary>
/// <typeparam name="TPlatformArgs">The platform-specific argument type</typeparam>
/// <typeparam name="TEvent">The resulting cross-platform event type</typeparam>
public interface IEventAdapter<in TPlatformArgs, out TEvent>
    where TEvent : INearbyConnectionsEvent
{
    /// <summary>
    /// Transforms platform arguments into a cross-platform event.
    /// </summary>
    /// <param name="platformArgs">Platform-specific arguments</param>
    /// <returns>Cross-platform event or null if transformation should be skipped</returns>
    TEvent? Transform(TPlatformArgs platformArgs);
}