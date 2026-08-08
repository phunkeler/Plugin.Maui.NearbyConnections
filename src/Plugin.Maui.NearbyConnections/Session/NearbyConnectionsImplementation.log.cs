namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    // -------------------------------------------------------------------------
    // Pump failures
    //
    // These are the only place a start failure or a background-loop fault can be
    // observed — nothing awaits the pumps. A silent catch here is invisible by
    // construction, so every one of them logs.
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Error, Message = "Advertising stopped unexpectedly. Advertising is no longer active.")]
    partial void LogAdvertisePumpFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Discovery stopped unexpectedly. Discovery is no longer active.")]
    partial void LogDiscoverPumpFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to observe disconnect for device {DeviceId}. ConnectionDropped may not have been raised.")]
    partial void LogDisconnectWatchFailed(string deviceId, Exception exception);

    // -------------------------------------------------------------------------
    // Consumer callbacks
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Error, Message = "A handler for {EventName} threw. Remaining handlers for this event did not run.")]
    partial void LogEventHandlerFailed(string eventName, Exception exception);

    // -------------------------------------------------------------------------
    // Misuse guardrails
    //
    // ConnectionEstablished does not replay, so a consumer constructed after a
    // connection opens never starts a receive loop for it. Payloads are then
    // written to an unbounded channel nobody reads and are lost with no error
    // anywhere — a failure mode that is invisible by construction and has
    // already cost one debugging session. These two warnings are the only
    // signal that it is happening.
    // -------------------------------------------------------------------------

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Connection to device {DeviceId} was established, but nothing is subscribed to ConnectionEstablished. " +
            "Inbound payloads will be buffered and never observed. This event does not replay: register the consumer that " +
            "calls NearbyConnection.ReceiveAsync as an IMauiInitializeService so it exists before the first connection. " +
            "See docs/PAYLOAD-DELIVERY.md.")]
    partial void LogNoConnectionEstablishedSubscribers(string deviceId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The handshake with device {DeviceId} ended before a connection was established: {Reason}.")]
    partial void LogHandshakeEnded(string deviceId, EndReason reason);

    // -------------------------------------------------------------------------
    // Teardown
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to disconnect device {DeviceId} while stopping the session. Teardown continued.")]
    partial void LogStopConnectionError(string deviceId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to reject the outstanding request from device {DeviceId} while stopping the session. Teardown continued.")]
    partial void LogStopRejectError(string deviceId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to stop the session cleanly during disposal.")]
    partial void LogDisposeError(Exception exception);
}
