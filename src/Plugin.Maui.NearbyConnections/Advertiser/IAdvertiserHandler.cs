namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Receives <see cref="AdvertiserEvent"/> notifications from a <see cref="INearbyAdvertiser"/> stream.
/// All <c>On*</c> methods have default no-op implementations; override only the events you care about.
/// </summary>
public interface IAdvertiserHandler
{
    /// <summary>
    /// Gets an optional dispatcher used to marshal all <c>On*</c> invocations to the UI thread.
    /// Return <see langword="null"/> to invoke handlers on the channel reader thread (background).
    /// </summary>
    IDispatcher? Dispatcher => null;

    /// <summary>
    /// Called when an inbound connection request arrives and is awaiting accept or reject.
    /// </summary>
    /// <param name="ev">The event carrying the pending connection request.</param>
    void OnConnectionRequested(AdvertiserEvent.ConnectionRequested ev) { }

    /// <summary>
    /// Called when a pending request was accepted and is now an active connection.
    /// </summary>
    /// <param name="ev">The event carrying the accepted connection.</param>
    void OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev) { }

    /// <summary>
    /// Called when an active connection terminated from either side.
    /// </summary>
    /// <param name="ev">The event carrying the dropped connection.</param>
    void OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev) { }

    /// <summary>
    /// Called when a payload is received from an active connection.
    /// </summary>
    /// <param name="ev">The event carrying the connection and payload.</param>
    void OnPayloadReceived(AdvertiserEvent.PayloadReceived ev) { }

    /// <summary>
    /// Called once after all current-state events have been replayed.
    /// Every event before this is synthetic; every event after this is live.
    /// </summary>
    /// <param name="ev">The synchronized sentinel event.</param>
    void OnSynchronized(AdvertiserEvent.Synchronized ev) { }
}
