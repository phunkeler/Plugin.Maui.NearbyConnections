namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Receives <see cref="DiscovererEvent"/> notifications from a <see cref="INearbyDiscoverer"/> stream.
/// All <c>On*</c> methods have default no-op implementations; override only the events you care about.
/// </summary>
public interface IDiscovererHandler
{
    /// <summary>
    /// Gets an optional dispatcher used to marshal all <c>On*</c> invocations to the UI thread.
    /// Return <see langword="null"/> to invoke handlers directly on the channel reader thread —
    /// a background thread on both platforms; do not assume the UI thread. Supply a dispatcher
    /// to marshal handler invocations to the UI thread.
    /// </summary>
    IDispatcher? Dispatcher => null;

    /// <summary>
    /// Called when a nearby device became visible during discovery.
    /// </summary>
    /// <param name="ev">The event carrying the found device.</param>
    Task OnDeviceFound(DiscovererEvent.DeviceFound ev) => Task.CompletedTask;

    /// <summary>
    /// Called when a previously visible device is no longer reachable.
    /// </summary>
    /// <param name="ev">The event carrying the lost device.</param>
    Task OnDeviceLost(DiscovererEvent.DeviceLost ev) => Task.CompletedTask;

    /// <summary>
    /// Called when a connection to a nearby device was successfully established.
    /// </summary>
    /// <param name="ev">The event carrying the new connection.</param>
    Task OnDeviceConnected(DiscovererEvent.DeviceConnected ev) => Task.CompletedTask;

    /// <summary>
    /// Called when an active connection terminated from either side.
    /// </summary>
    /// <param name="ev">The event carrying the dropped connection.</param>
    Task OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev) => Task.CompletedTask;

    /// <summary>
    /// Called when a payload is received from an active connection.
    /// </summary>
    /// <param name="ev">The event carrying the connection and payload.</param>
    Task OnPayloadReceived(DiscovererEvent.PayloadReceived ev) => Task.CompletedTask;

    /// <summary>
    /// Called once after all current-state events have been replayed.
    /// Every event before this is synthetic; every event after this is live.
    /// </summary>
    /// <param name="ev">The synchronized sentinel event.</param>
    Task OnSynchronized(DiscovererEvent.Synchronized ev) => Task.CompletedTask;
}
