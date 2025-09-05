using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The high-level session object.
/// </summary>
public interface INearbyConnectionsSession : IDisposable
{
    /// <summary>
    /// A provider for pushed-based notifications.
    /// </summary>
    IObservable<INearbyConnectionsEvent> Events { get; }
}