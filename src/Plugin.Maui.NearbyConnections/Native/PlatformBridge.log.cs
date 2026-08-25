namespace Plugin.Maui.NearbyConnections;

// The level contract for this library is documented for consumers in docs/LOGGING.md. In short:
// Trace = per-payload, Debug = per-device, Information = state changes the app cannot otherwise
// observe, Warning = recovered, Error = an operation is degraded. Keep new messages consistent
// with that table, and update the doc when adding a message a consumer would filter on.
sealed partial class PlatformBridge
{
    // -------------------------------------------------------------------------
    // Shared failure shapes
    //
    // These three replace what were 25 near-identical declarations differing only in a hardcoded
    // method name. The name is now a {Callback}/{Writer} property, so a consumer alerting on
    // "a platform callback threw" writes one EventId filter instead of thirteen, and structured
    // sinks can still group by name. Pass the name with nameof() — it is a compile-time constant,
    // so this costs nothing at runtime and cannot drift from the method it names.
    // -------------------------------------------------------------------------

    [LoggerMessage(
        EventId = 2027,
        Level = LogLevel.Error,
        Message = "Platform callback {Callback} failed for device {DeviceId}. The event it carried was lost.")]
    internal partial void LogCallbackError(string callback, string deviceId, Exception ex);

    [LoggerMessage(
        EventId = 2070,
        Level = LogLevel.Debug,
        Message = "{Writer}: the stream was already completed, so the event for device {DeviceId} was dropped.")]
    internal partial void LogWriteChannelCompleted(string writer, string deviceId);

    [LoggerMessage(
        EventId = 2071,
        Level = LogLevel.Error,
        Message = "{Writer}: unexpected error handling the event for device {DeviceId}.")]
    internal partial void LogWriteError(string writer, string deviceId, Exception ex);

    // -------------------------------------------------------------------------
    // Devices
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "Device found: Id={DeviceId}, DisplayName={DisplayName}")]
    internal partial void LogDeviceFound(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "Device lost: Id={DeviceId}, DisplayName={DisplayName}")]
    internal partial void LogDeviceLost(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug, Message = "Device disconnected: Id={DeviceId}")]
    internal partial void LogDeviceDisconnected(string deviceId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Debug, Message = "Connected device stopped advertising, connection remains: Id={DeviceId}, DisplayName={DisplayName}")]
    internal partial void LogConnectedDeviceStoppedAdvertising(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "No peer found for device: Id={DeviceId}, DisplayName={DisplayName}")]
    internal partial void LogNoPeerFoundForDevice(string deviceId, string? displayName);

    // -------------------------------------------------------------------------
    // Connections
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2010, Level = LogLevel.Debug, Message = "Connection request received from: Id={DeviceId}, DisplayName={DisplayName}")]
    internal partial void LogConnectionRequestReceived(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Debug, Message = "Disconnecting from device: Id={DeviceId}, DisplayName={DisplayName}")]
    internal partial void LogDisconnecting(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Warning, Message = "Failed to clear platform state for the timed-out connection attempt to device {DeviceId}. A retry may fail until the platform releases the endpoint.")]
    internal partial void LogAbandonConnectError(string deviceId, Exception exception);

    // -------------------------------------------------------------------------
    // Android-specific
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2020, Level = LogLevel.Debug, Message = "Connection result: DeviceId={DeviceId}, StatusCode={StatusCode}, StatusMessage={StatusMessage}, IsSuccess={IsSuccess}")]
    internal partial void LogConnectionResult(string deviceId, int statusCode, string statusMessage, bool isSuccess);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Trace, Message = "Payload received: DeviceId={DeviceId}, PayloadId={PayloadId}, PayloadType={PayloadType}")]
    internal partial void LogPayloadReceived(string deviceId, long payloadId, int payloadType);

    [LoggerMessage(EventId = 2022, Level = LogLevel.Trace, Message = "Payload transfer update: DeviceId={DeviceId}, PayloadId={PayloadId}, Status={Status}, TotalBytes={TotalBytes}, BytesTransferred={BytesTransferred}")]
    internal partial void LogPayloadTransferUpdate(string deviceId, long payloadId, int status, long totalBytes, long bytesTransferred);

    [LoggerMessage(EventId = 2023, Level = LogLevel.Warning, Message = "Cannot send file: '{Uri}' is not a valid URI. Only 'file://' and 'content://' schemes are supported.")]
    internal partial void LogInvalidFileUri(string uri);

    [LoggerMessage(EventId = 2024, Level = LogLevel.Warning, Message = "Could not resolve display name from content URI.")]
    internal partial void LogCouldNotResolveContentUriName(Exception error);

    [LoggerMessage(EventId = 2025, Level = LogLevel.Error, Message = "Failed to build file payload.")]
    internal partial void LogBuildFilePayloadFailed(Exception error);

    [LoggerMessage(EventId = 2026, Level = LogLevel.Error, Message = "Failed to process incoming payload: DeviceId={DeviceId}, PayloadId={PayloadId}")]
    internal partial void LogIncomingPayloadProcessingFailed(string deviceId, long payloadId);

    [LoggerMessage(EventId = 2029, Level = LogLevel.Warning, Message = "Failed to clear stale connection state for endpoint: DeviceId={DeviceId}")]
    internal partial void LogFailedToClearStaleConnectionState(string deviceId, Exception ex);

    // -------------------------------------------------------------------------
    // iOS-specific
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2040, Level = LogLevel.Error, Message = "Advertising failed to start.")]
    internal partial void LogDidNotStartAdvertising(Exception error);

    [LoggerMessage(EventId = 2041, Level = LogLevel.Error, Message = "Discovery failed to start.")]
    internal partial void LogDidNotStartBrowsing(Exception error);

    [LoggerMessage(EventId = 2042, Level = LogLevel.Trace, Message = "Resource transfer progress: Id={DeviceId}, Direction={Direction}, PayloadId={PayloadId}, TotalBytes={TotalBytes}, BytesTransferred={BytesTransferred}")]
    internal partial void LogResourceTransferProgress(string deviceId, string direction, long payloadId, long totalBytes, long bytesTransferred);

    [LoggerMessage(EventId = 2048, Level = LogLevel.Error, Message = "Failed to send bytes to peer: DisplayName={DisplayName}")]
    internal partial void LogSendBytesFailed(string? displayName, Exception error);

    [LoggerMessage(EventId = 2049, Level = LogLevel.Warning, Message = "File transfer stalled: Id={DeviceId}, DisplayName={DisplayName}, Timeout={TimeoutSeconds}s")]
    internal partial void LogSendFileTimeout(string deviceId, string? displayName, double timeoutSeconds);

    [LoggerMessage(EventId = 2050, Level = LogLevel.Error, Message = "File transfer failed: Id={DeviceId}, DisplayName={DisplayName}")]
    internal partial void LogSendFileFailed(string deviceId, string? displayName, Exception error);

    [LoggerMessage(EventId = 2051, Level = LogLevel.Debug, Message = "Last peer disconnected, session disposed.")]
    internal partial void LogSessionDisposed();


    // 2052 (LogPeerStateChanged) and 2054 (LogControlMessageReceived) are declared in
    // PlatformBridge.log.ios.cs — they take iOS-only/internal enum parameters. Ids stay reserved here.

    [LoggerMessage(EventId = 2053, Level = LogLevel.Trace, Message = "Data received from peer: Id={DeviceId}, DisplayName={DisplayName}, Length={Length} bytes")]
    internal partial void LogDataReceived(string deviceId, string? displayName, long length);

    // 2055 (LogDisconnectingFromSession) is retired. It reported a session-wide teardown on an
    // inbound Disconnect frame, which was the bug: departure is now per-peer (EventId 2092).
    // The id stays reserved and is never reused.

    [LoggerMessage(EventId = 2092, Level = LogLevel.Debug, Message = "Peer announced disconnect via control message: DeviceId={DeviceId}")]
    internal partial void LogPeerDisconnectRequested(string deviceId);

    [LoggerMessage(
        EventId = 2093,
        Level = LogLevel.Warning,
        Message = "Disposal stopped waiting for queued work on {PendingPeerCount} peers after {TimeoutSeconds}s. A staged file a copy was still writing may be deleted by the sweep that follows.")]
    internal partial void LogPayloadDrainTimedOut(int pendingPeerCount, double timeoutSeconds);

    [LoggerMessage(
        EventId = 2094,
        Level = LogLevel.Warning,
        Message = "Releasing connection {DeviceId} stopped waiting for its queued work after {TimeoutSeconds}s. The payload handles are freed anyway, so an inbound copy that is still running may fail.")]
    internal partial void LogConnectionDrainTimedOut(string deviceId, double timeoutSeconds);

    [LoggerMessage(EventId = 2056, Level = LogLevel.Warning, Message = "Unknown control message type: {Type}")]
    internal partial void LogUnknownControlMessageType(object type);

    [LoggerMessage(EventId = 2057, Level = LogLevel.Debug, Message = "Started receiving resource from: Id={DeviceId}, DisplayName={DisplayName}, ResourceName={ResourceName}")]
    internal partial void LogResourceReceiveStarted(string deviceId, string? displayName, string resourceName);

    [LoggerMessage(EventId = 2058, Level = LogLevel.Debug, Message = "Finished receiving resource from: Id={DeviceId}, DisplayName={DisplayName}, ResourceName={ResourceName}, Location={Location}, Error={Error}")]
    internal partial void LogResourceReceiveFinished(string deviceId, string? displayName, string resourceName, string? location, string? error);

    [LoggerMessage(EventId = 2059, Level = LogLevel.Error, Message = "Failed to copy received file: Source={Source}, Destination={Destination}")]
    internal partial void LogFileCopyFailed(string source, string destination, Exception error);

    [LoggerMessage(EventId = 2060, Level = LogLevel.Error, Message = "Failed to delete temporary received file: Path={Path}")]
    internal partial void LogFileDeleteFailed(string path, Exception error);

    [LoggerMessage(EventId = 2061, Level = LogLevel.Debug, Message = "Canceled copy of received file during teardown: Source={Source}, Destination={Destination}")]
    internal partial void LogFileCopyCanceled(string source, string destination);

    // -------------------------------------------------------------------------
    // Channel bridge helpers
    // -------------------------------------------------------------------------

    // 2070 (LogWriteChannelCompleted) and 2071 (LogWriteError) are declared at the top of this
    // file as shared failure shapes. Ids stay reserved here.

    // Logged once per connection, not once per payload: this fires on a hot path, and a consumer
    // that never called ReceiveAsync would otherwise produce one warning for every message received.
    [LoggerMessage(
        EventId = 2079,
        Level = LogLevel.Warning,
        Message = "A payload arrived from peer {DeviceId} but ReceiveAsync was never called for this connection, so it " +
            "cannot be observed. Payloads are buffered and lost. Start consuming the connection when the device " +
            "reports Connected, and register that consumer so it exists before the first connection. " +
            "See docs/PAYLOAD-DELIVERY.md.")]
    internal partial void LogPayloadArrivedUnobserved(string deviceId);

    [LoggerMessage(EventId = 2080, Level = LogLevel.Warning, Message = "WritePayload: no active connection for peer {DeviceId}; payload dropped.")]
    internal partial void LogWritePayloadNoConnection(string deviceId);

    [LoggerMessage(EventId = 2081, Level = LogLevel.Error, Message = "DisposeAsync: error disposing connection to peer {DeviceId}; continuing teardown.")]
    internal partial void LogDisposeConnectionError(string deviceId, Exception ex);

    [LoggerMessage(EventId = 2082, Level = LogLevel.Error, Message = "Failed to start advertising.")]
    internal partial void LogStartAdvertisingFailed(Exception ex);

    [LoggerMessage(EventId = 2083, Level = LogLevel.Error, Message = "Failed to start discovery.")]
    internal partial void LogStartDiscoveringFailed(Exception ex);

    [LoggerMessage(EventId = 2084, Level = LogLevel.Error, Message = "Advertise start failure could not be delivered: the advertise stream was already completed. The consumer will observe a normal end of stream instead of this error.")]
    internal partial void LogStartAdvertisingFaultDropped();

    [LoggerMessage(EventId = 2085, Level = LogLevel.Error, Message = "Discovery start failure could not be delivered: the discover stream was already completed. The consumer will observe a normal end of stream instead of this error.")]
    internal partial void LogStartDiscoveringFaultDropped();

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2090, Level = LogLevel.Warning, Message = "Could not determine {Condition} while checking availability; it is reported as satisfied.")]
    internal partial void LogAvailabilityCheckPartiallyFailed(string condition, Exception ex);

    [LoggerMessage(EventId = 2091, Level = LogLevel.Error, Message = "ServiceId '{ServiceId}' is not valid for MultipeerConnectivity and would crash the process if used to start. {Failures}")]
    internal partial void LogAvailabilityInvalidServiceId(string serviceId, string failures);
}