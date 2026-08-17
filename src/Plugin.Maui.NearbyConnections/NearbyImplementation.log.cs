namespace Plugin.Maui.NearbyConnections;

// EventId ranges (stable across edits — assign the next free id in a type's range rather than
// renumbering; never reuse an id once shipped):
//   NearbyImplementation (this file)        1000-1099
//   PlatformNearby                          2000-2099
//   iOS identity/lifecycle (PeerRegistry — peer keys, local peer
//     identity, handle tracking; AppLifecycleObserver)  3000-3099
sealed partial class NearbyImplementation
{
    // -------------------------------------------------------------------------
    // Pump failures
    //
    // These are the only place a start failure or a background-loop fault can be
    // observed — nothing awaits the pumps. A silent catch here is invisible by
    // construction, so every one of them logs.
    // -------------------------------------------------------------------------

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Advertising stopped unexpectedly. Advertising is no longer active.")]
    partial void LogAdvertisePumpFailed(Exception exception);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Discovery stopped unexpectedly. Discovery is no longer active.")]
    partial void LogDiscoverPumpFailed(Exception exception);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error, Message = "Discovery refresh failed. Devices that have gone out of range may linger until discovery is restarted.")]
    partial void LogRefreshDiscoveryFailed(Exception exception);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Failed to observe disconnect for device {DeviceId}. It may be left reporting Connected.")]
    partial void LogDisconnectWatchFailed(string deviceId, Exception exception);

    // -------------------------------------------------------------------------
    // Handshake outcomes
    // -------------------------------------------------------------------------

    // Debug, not Information: a handshake ending without a connection is an ordinary outcome —
    // the remote side rejected, or the attempt was abandoned. Information is on by default in a
    // consumer's app, and this fires per attempt, so it belongs below the default threshold.
    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Debug,
        Message = "The handshake with device {DeviceId} ended before a connection was established: {Reason}.")]
    partial void LogHandshakeEnded(string deviceId, EndReason reason);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Error,
        Message = "Automatically accepting the connection request from device {DeviceId} failed. " +
            "No application code initiated this accept, so there is no caller to observe the " +
            "failure: it is reported here only.")]
    partial void LogAutoAcceptFailed(string deviceId, Exception exception);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Debug,
        Message = "The inbound connection request from device {DeviceId} was not answered within {TimeoutSeconds}s and was rejected.")]
    partial void LogInboundRequestExpired(string deviceId, double timeoutSeconds);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Warning,
        Message = "Failed to reject the expired connection request from device {DeviceId}. " +
            "The device was returned to Visible regardless, but the platform may still hold the request open.")]
    partial void LogInboundRequestExpiryRejectFailed(string deviceId, Exception exception);

    [LoggerMessage(
        EventId = 1014,
        Level = LogLevel.Error,
        Message = "The expiry countdown for the connection request from device {DeviceId} failed. " +
            "The request may be left outstanding until the session stops.")]
    partial void LogInboundRequestExpiryFailed(string deviceId, Exception exception);

    // -------------------------------------------------------------------------
    // Teardown
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 1020, Level = LogLevel.Warning, Message = "Failed to disconnect device {DeviceId} while stopping the session. Teardown continued.")]
    partial void LogStopConnectionError(string deviceId, Exception exception);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Warning, Message = "Failed to reject the outstanding request from device {DeviceId} while stopping the session. Teardown continued.")]
    partial void LogStopRejectError(string deviceId, Exception exception);

    [LoggerMessage(EventId = 1022, Level = LogLevel.Error, Message = "Failed to stop the session cleanly during disposal.")]
    partial void LogDisposeError(Exception exception);
}