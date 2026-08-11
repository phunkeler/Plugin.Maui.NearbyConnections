namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyImplementation
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Discovery refresh failed. Devices that have gone out of range may linger until discovery is restarted.")]
    partial void LogRefreshDiscoveryFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to observe disconnect for device {DeviceId}. It may be left reporting Connected.")]
    partial void LogDisconnectWatchFailed(string deviceId, Exception exception);

    // -------------------------------------------------------------------------
    // Handshake outcomes
    // -------------------------------------------------------------------------

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The handshake with device {DeviceId} ended before a connection was established: {Reason}.")]
    partial void LogHandshakeEnded(string deviceId, EndReason reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Automatically accepting the connection request from device {DeviceId} failed. " +
            "No application code initiated this accept, so there is no caller to observe the " +
            "failure: it is reported here only.")]
    partial void LogAutoAcceptFailed(string deviceId, Exception exception);

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
