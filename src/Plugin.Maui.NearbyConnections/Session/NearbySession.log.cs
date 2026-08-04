namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbySession
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
    // Teardown
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to disconnect device {DeviceId} while stopping the session. Teardown continued.")]
    partial void LogStopConnectionError(string deviceId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to reject the outstanding request from device {DeviceId} while stopping the session. Teardown continued.")]
    partial void LogStopRejectError(string deviceId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to stop the session cleanly during disposal.")]
    partial void LogDisposeError(Exception exception);
}
