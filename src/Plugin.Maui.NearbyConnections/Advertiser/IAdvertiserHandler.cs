namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Receives <see cref="AdvertiserEvent"/> notifications from a <see cref="INearbyAdvertiser"/> stream.
/// All <c>On*</c> methods have default no-op implementations; override only the events you care about.
/// </summary>
public interface IAdvertiserHandler
{
    /// <summary>
    /// Gets an optional dispatcher used to marshal all <c>On*</c> invocations to the UI thread.
    /// Return <see langword="null"/> to invoke handlers directly on the channel reader thread —
    /// a background thread on both platforms; do not assume the UI thread. Supply a dispatcher
    /// to marshal handler invocations to the UI thread.
    /// </summary>
    IDispatcher? Dispatcher => null;

    /// <summary>
    /// Called when an inbound connection request arrives and is awaiting accept or reject.
    /// </summary>
    /// <param name="ev">The event carrying the pending connection request.</param>
    Task OnConnectionRequested(AdvertiserEvent.ConnectionRequested ev) => Task.CompletedTask;

    /// <summary>
    /// Called when a pending request was accepted and is now an active connection.
    /// </summary>
    /// <param name="ev">The event carrying the accepted connection.</param>
    Task OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev) => Task.CompletedTask;

    /// <summary>
    /// Called when an active connection terminated from either side.
    /// </summary>
    /// <param name="ev">The event carrying the dropped connection.</param>
    Task OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev) => Task.CompletedTask;

    /// <summary>
    /// Called when a pending connection request was discarded because advertising stopped
    /// before it was accepted or rejected.
    /// </summary>
    /// <param name="ev">The event carrying the expired request.</param>
    Task OnConnectionRequestExpired(AdvertiserEvent.ConnectionRequestExpired ev) => Task.CompletedTask;

    /// <summary>
    /// Called when a payload is received from an active connection.
    /// </summary>
    /// <param name="ev">The event carrying the connection and payload.</param>
    Task OnPayloadReceived(AdvertiserEvent.PayloadReceived ev) => Task.CompletedTask;

    /// <summary>
    /// Called once after all current-state events have been replayed.
    /// Every event before this is synthetic; every event after this is live.
    /// </summary>
    /// <param name="ev">The synchronized sentinel event.</param>
    Task OnSynchronized(AdvertiserEvent.Synchronized ev) => Task.CompletedTask;
}
