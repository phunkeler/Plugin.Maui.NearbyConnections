namespace Plugin.Maui.NearbyConnections;

// The level contract for this library is documented for consumers in docs/LOGGING.md. In short:
// Trace = per-payload, Debug = per-device, Information = state changes the app cannot otherwise
// observe, Warning = recovered, Error = an operation is degraded. Keep new messages consistent
// with that table, and update the doc when adding a message a consumer would filter on.
sealed partial class PlatformNearby
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
    partial void LogCallbackError(string callback, string deviceId, Exception ex);

    [LoggerMessage(
        EventId = 2070,
        Level = LogLevel.Debug,
        Message = "{Writer}: the stream was already completed, so the event for device {DeviceId} was dropped.")]
    partial void LogWriteChannelCompleted(string writer, string deviceId);

    [LoggerMessage(
        EventId = 2071,
        Level = LogLevel.Error,
        Message = "{Writer}: unexpected error handling the event for device {DeviceId}.")]
    partial void LogWriteError(string writer, string deviceId, Exception ex);

    // -------------------------------------------------------------------------
    // Devices
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "Device found: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDeviceFound(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "Device lost: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDeviceLost(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug, Message = "Device disconnected: Id={DeviceId}")]
    partial void LogDeviceDisconnected(string deviceId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Debug, Message = "Connected device stopped advertising, connection remains: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectedDeviceStoppedAdvertising(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "No peer found for device: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogNoPeerFoundForDevice(string deviceId, string? displayName);

    // -------------------------------------------------------------------------
    // Connections
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2010, Level = LogLevel.Debug, Message = "Connection request received from: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectionRequestReceived(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Debug, Message = "Disconnecting from device: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDisconnecting(string deviceId, string? displayName);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Warning, Message = "Failed to clear platform state for the timed-out connection attempt to device {DeviceId}. A retry may fail until the platform releases the endpoint.")]
    partial void LogAbandonConnectError(string deviceId, Exception exception);

    // -------------------------------------------------------------------------
    // Android-specific
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2020, Level = LogLevel.Debug, Message = "Connection result: EndpointId={EndpointId}, StatusCode={StatusCode}, StatusMessage={StatusMessage}, IsSuccess={IsSuccess}")]
    partial void LogConnectionResult(string endpointId, int statusCode, string statusMessage, bool isSuccess);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Trace, Message = "Payload received: EndpointId={EndpointId}, PayloadId={PayloadId}, PayloadType={PayloadType}")]
    partial void LogPayloadReceived(string endpointId, long payloadId, int payloadType);

    [LoggerMessage(EventId = 2022, Level = LogLevel.Trace, Message = "Payload transfer update: EndpointId={EndpointId}, PayloadId={PayloadId}, Status={Status}, TotalBytes={TotalBytes}, BytesTransferred={BytesTransferred}")]
    partial void LogPayloadTransferUpdate(string endpointId, long payloadId, int status, long totalBytes, long bytesTransferred);

    [LoggerMessage(EventId = 2023, Level = LogLevel.Warning, Message = "Cannot send file: '{Uri}' is not a valid URI. Only 'file://' and 'content://' schemes are supported.")]
    partial void LogInvalidFileUri(string uri);

    [LoggerMessage(EventId = 2024, Level = LogLevel.Warning, Message = "Could not resolve display name from content URI.")]
    partial void LogCouldNotResolveContentUriName(Exception error);

    [LoggerMessage(EventId = 2025, Level = LogLevel.Error, Message = "Failed to build file payload.")]
    partial void LogBuildFilePayloadFailed(Exception error);

    [LoggerMessage(EventId = 2026, Level = LogLevel.Error, Message = "Failed to process incoming payload: EndpointId={EndpointId}, PayloadId={PayloadId}")]
    partial void LogIncomingPayloadProcessingFailed(string endpointId, long payloadId);

    [LoggerMessage(EventId = 2029, Level = LogLevel.Warning, Message = "Failed to clear stale connection state for endpoint: EndpointId={EndpointId}")]
    partial void LogFailedToClearStaleConnectionState(string endpointId, Exception ex);

    // -------------------------------------------------------------------------
    // iOS-specific
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2040, Level = LogLevel.Error, Message = "Advertising failed to start.")]
    partial void LogDidNotStartAdvertising(Exception error);

    [LoggerMessage(EventId = 2041, Level = LogLevel.Error, Message = "Discovery failed to start.")]
    partial void LogDidNotStartBrowsing(Exception error);

    [LoggerMessage(EventId = 2048, Level = LogLevel.Error, Message = "Failed to send bytes to peer: DisplayName={DisplayName}")]
    partial void LogSendBytesFailed(string displayName, Exception error);

    [LoggerMessage(EventId = 2049, Level = LogLevel.Warning, Message = "File transfer stalled: Id={DeviceId}, DisplayName={DisplayName}, Timeout={TimeoutSeconds}s")]
    partial void LogSendFileTimeout(string deviceId, string? displayName, double timeoutSeconds);

    [LoggerMessage(EventId = 2050, Level = LogLevel.Error, Message = "File transfer failed: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogSendFileFailed(string deviceId, string? displayName, Exception error);

    [LoggerMessage(EventId = 2051, Level = LogLevel.Debug, Message = "Last peer disconnected, session disposed.")]
    partial void LogSessionDisposed();

    // 2052 (LogPeerStateChanged) and 2054 (LogControlMessageReceived) are declared in
    // PlatformNearby.log.ios.cs — they take iOS-only/internal enum parameters. Ids stay reserved here.

    [LoggerMessage(EventId = 2053, Level = LogLevel.Trace, Message = "Data received from peer: Id={DeviceId}, DisplayName={DisplayName}, Length={Length} bytes")]
    partial void LogDataReceived(string deviceId, string displayName, long length);

    [LoggerMessage(EventId = 2055, Level = LogLevel.Debug, Message = "Disconnecting from session due to control message.")]
    partial void LogDisconnectingFromSession();

    [LoggerMessage(EventId = 2056, Level = LogLevel.Warning, Message = "Unknown control message type: {Type}")]
    partial void LogUnknownControlMessageType(object type);

    [LoggerMessage(EventId = 2057, Level = LogLevel.Debug, Message = "Started receiving resource from: Id={DeviceId}, DisplayName={DisplayName}, ResourceName={ResourceName}")]
    partial void LogResourceReceiveStarted(string deviceId, string displayName, string resourceName);

    [LoggerMessage(EventId = 2058, Level = LogLevel.Debug, Message = "Finished receiving resource from: Id={DeviceId}, DisplayName={DisplayName}, ResourceName={ResourceName}, Location={Location}, Error={Error}")]
    partial void LogResourceReceiveFinished(string deviceId, string displayName, string resourceName, string? location, string? error);

    [LoggerMessage(EventId = 2059, Level = LogLevel.Error, Message = "Failed to copy received file: Source={Source}, Destination={Destination}")]
    partial void LogFileCopyFailed(string source, string destination, Exception error);

    [LoggerMessage(EventId = 2060, Level = LogLevel.Error, Message = "Failed to delete temporary received file: Path={Path}")]
    partial void LogFileDeleteFailed(string path, Exception error);

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
        Message = "A payload arrived from peer {PeerId} but ReceiveAsync was never called for this connection, so it " +
            "cannot be observed. Payloads are buffered and lost. Start consuming the connection when the device " +
            "reports Connected, and register that consumer so it exists before the first connection. " +
            "See docs/PAYLOAD-DELIVERY.md.")]
    partial void LogPayloadArrivedUnobserved(string peerId);

    [LoggerMessage(EventId = 2080, Level = LogLevel.Warning, Message = "WritePayload: no active connection for peer {PeerId}; payload dropped.")]
    partial void LogWritePayloadNoConnection(string peerId);

    [LoggerMessage(EventId = 2081, Level = LogLevel.Error, Message = "DisposeAsync: error disposing connection to peer {PeerId}; continuing teardown.")]
    partial void LogDisposeConnectionError(string peerId, Exception ex);

    [LoggerMessage(EventId = 2082, Level = LogLevel.Error, Message = "Failed to start advertising.")]
    partial void LogStartAdvertisingFailed(Exception ex);

    [LoggerMessage(EventId = 2083, Level = LogLevel.Error, Message = "Failed to start discovery.")]
    partial void LogStartDiscoveringFailed(Exception ex);

    [LoggerMessage(EventId = 2084, Level = LogLevel.Error, Message = "Advertise start failure could not be delivered: the advertise stream was already completed. The consumer will observe a normal end of stream instead of this error.")]
    partial void LogStartAdvertisingFaultDropped();

    [LoggerMessage(EventId = 2085, Level = LogLevel.Error, Message = "Discovery start failure could not be delivered: the discover stream was already completed. The consumer will observe a normal end of stream instead of this error.")]
    partial void LogStartDiscoveringFaultDropped();

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2090, Level = LogLevel.Warning, Message = "Could not determine {Condition} while checking availability; it is reported as satisfied.")]
    partial void LogAvailabilityCheckPartiallyFailed(string condition, Exception ex);

    [LoggerMessage(EventId = 2091, Level = LogLevel.Error, Message = "ServiceId '{ServiceId}' is not valid for MultipeerConnectivity and would crash the process if used to start. {Failures}")]
    partial void LogAvailabilityInvalidServiceId(string serviceId, string failures);
}