namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Receives <see cref="DiscovererEvent"/> notifications from a <see cref="INearbyDiscoverer"/> stream.
/// All <c>On*</c> methods have default no-op implementations; override only the events you care about.
/// </summary>
public interface IDiscovererHandler
{
    /// <summary>
    /// Gets an optional dispatcher used to marshal all <c>On*</c> invocations to the UI thread.
    /// Return <see langword="null"/> to invoke handlers on the channel reader thread (background).
    /// </summary>
    IDispatcher? Dispatcher => null;

    /// <summary>
    /// Called when a nearby device became visible during discovery.
    /// </summary>
    /// <param name="ev">The event carrying the found device.</param>
    void OnDeviceFound(DiscovererEvent.DeviceFound ev) { }

    /// <summary>
    /// Called when a previously visible device is no longer reachable.
    /// </summary>
    /// <param name="ev">The event carrying the lost device.</param>
    void OnDeviceLost(DiscovererEvent.DeviceLost ev) { }

    /// <summary>
    /// Called when a connection to a nearby device was successfully established.
    /// </summary>
    /// <param name="ev">The event carrying the new connection.</param>
    void OnDeviceConnected(DiscovererEvent.DeviceConnected ev) { }

    /// <summary>
    /// Called when an active connection terminated from either side.
    /// </summary>
    /// <param name="ev">The event carrying the dropped connection.</param>
    void OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev) { }

    /// <summary>
    /// Called when a payload is received from an active connection.
    /// </summary>
    /// <param name="ev">The event carrying the connection and payload.</param>
    void OnPayloadReceived(DiscovererEvent.PayloadReceived ev) { }

    /// <summary>
    /// Called once after all current-state events have been replayed.
    /// Every event before this is synthetic; every event after this is live.
    /// </summary>
    /// <param name="ev">The synchronized sentinel event.</param>
    void OnSynchronized(DiscovererEvent.Synchronized ev) { }
}
