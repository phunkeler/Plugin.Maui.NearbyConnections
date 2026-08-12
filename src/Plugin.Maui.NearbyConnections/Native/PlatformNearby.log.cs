namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
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

    [LoggerMessage(EventId = 2027, Level = LogLevel.Error, Message = "OnConnectionInitiated callback error: EndpointId={EndpointId}")]
    partial void LogOnConnectionInitiatedError(string endpointId, Exception ex);

    [LoggerMessage(EventId = 2028, Level = LogLevel.Error, Message = "OnConnectionResult callback error: EndpointId={EndpointId}")]
    partial void LogOnConnectionResultError(string endpointId, Exception ex);

    [LoggerMessage(EventId = 2029, Level = LogLevel.Warning, Message = "Failed to clear stale connection state for endpoint: EndpointId={EndpointId}")]
    partial void LogFailedToClearStaleConnectionState(string endpointId, Exception ex);

    [LoggerMessage(EventId = 2030, Level = LogLevel.Error, Message = "OnDisconnected callback error: EndpointId={EndpointId}")]
    partial void LogOnDisconnectedError(string endpointId, Exception ex);

    [LoggerMessage(EventId = 2031, Level = LogLevel.Error, Message = "OnEndpointFound callback error: EndpointId={EndpointId}")]
    partial void LogOnEndpointFoundError(string endpointId, Exception ex);

    [LoggerMessage(EventId = 2032, Level = LogLevel.Error, Message = "OnEndpointLost callback error: EndpointId={EndpointId}")]
    partial void LogOnEndpointLostError(string endpointId, Exception ex);

    [LoggerMessage(EventId = 2033, Level = LogLevel.Error, Message = "OnPayloadReceived callback error: EndpointId={EndpointId}")]
    partial void LogOnPayloadReceivedError(string endpointId, Exception ex);

    [LoggerMessage(EventId = 2034, Level = LogLevel.Error, Message = "OnPayloadTransferUpdate callback error: EndpointId={EndpointId}")]
    partial void LogOnPayloadTransferUpdateError(string endpointId, Exception ex);

    // -------------------------------------------------------------------------
    // iOS-specific
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 2040, Level = LogLevel.Error, Message = "Advertising failed to start.")]
    partial void LogDidNotStartAdvertising(Exception error);

    [LoggerMessage(EventId = 2041, Level = LogLevel.Error, Message = "Discovery failed to start.")]
    partial void LogDidNotStartBrowsing(Exception error);

    [LoggerMessage(EventId = 2042, Level = LogLevel.Error, Message = "DidReceiveInvitationFromPeer callback error: DisplayName={DisplayName}")]
    partial void LogDidReceiveInvitationError(string displayName, Exception ex);

    [LoggerMessage(EventId = 2043, Level = LogLevel.Error, Message = "FoundPeer callback error: DisplayName={DisplayName}")]
    partial void LogFoundPeerError(string displayName, Exception ex);

    [LoggerMessage(EventId = 2044, Level = LogLevel.Error, Message = "LostPeer callback error: DisplayName={DisplayName}")]
    partial void LogLostPeerError(string displayName, Exception ex);

    [LoggerMessage(EventId = 2045, Level = LogLevel.Error, Message = "OnPeerStateChanged callback error: DisplayName={DisplayName}")]
    partial void LogOnPeerStateChangedError(string displayName, Exception ex);

    [LoggerMessage(EventId = 2046, Level = LogLevel.Error, Message = "OnDataReceived callback error: DisplayName={DisplayName}")]
    partial void LogOnDataReceivedError(string displayName, Exception ex);

    [LoggerMessage(EventId = 2047, Level = LogLevel.Error, Message = "OnResourceFinished callback error: DisplayName={DisplayName}")]
    partial void LogOnResourceFinishedError(string displayName, Exception ex);

    [LoggerMessage(EventId = 2048, Level = LogLevel.Error, Message = "Failed to send bytes to peer: DisplayName={DisplayName}")]
    partial void LogSendBytesFailed(string displayName, Exception error);

    [LoggerMessage(EventId = 2049, Level = LogLevel.Warning, Message = "File transfer stalled: Id={DeviceId}, DisplayName={DisplayName}, Timeout={TimeoutSeconds}s")]
    partial void LogSendFileTimeout(string deviceId, string? displayName, double timeoutSeconds);

    [LoggerMessage(EventId = 2050, Level = LogLevel.Error, Message = "File transfer failed: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogSendFileFailed(string deviceId, string? displayName, Exception error);

    [LoggerMessage(EventId = 2051, Level = LogLevel.Debug, Message = "Last peer disconnected, session disposed.")]
    partial void LogSessionDisposed();

    [LoggerMessage(EventId = 2052, Level = LogLevel.Debug, Message = "Peer state changed: Id={DeviceId}, DisplayName={DisplayName}, State={State}")]
    partial void LogPeerStateChanged(string deviceId, string displayName, string? state);

    [LoggerMessage(EventId = 2053, Level = LogLevel.Trace, Message = "Data received from peer: Id={DeviceId}, DisplayName={DisplayName}, Length={Length} bytes")]
    partial void LogDataReceived(string deviceId, string displayName, long length);

    [LoggerMessage(EventId = 2054, Level = LogLevel.Trace, Message = "Control message received from peer: Id={DeviceId}, DisplayName={DisplayName}, Type={Type}")]
    partial void LogControlMessageReceived(string deviceId, string displayName, string? type);

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

    [LoggerMessage(EventId = 2070, Level = LogLevel.Debug, Message = "WriteDeviceFound: discover channel already completed, dropping event for device {DeviceId}.")]
    partial void LogWriteDeviceFoundChannelCompleted(string deviceId);

    [LoggerMessage(EventId = 2071, Level = LogLevel.Error, Message = "WriteDeviceFound: unexpected error writing device-found event for device {DeviceId}.")]
    partial void LogWriteDeviceFoundError(string deviceId, Exception ex);

    [LoggerMessage(EventId = 2072, Level = LogLevel.Debug, Message = "WriteDeviceLost: discover channel already completed, dropping event for device {DeviceId}.")]
    partial void LogWriteDeviceLostChannelCompleted(string deviceId);

    [LoggerMessage(EventId = 2073, Level = LogLevel.Error, Message = "WriteDeviceLost: unexpected error writing device-lost event for device {DeviceId}.")]
    partial void LogWriteDeviceLostError(string deviceId, Exception ex);

    [LoggerMessage(EventId = 2074, Level = LogLevel.Debug, Message = "WriteConnectionRequest: advertise channel already completed, rejecting incoming connection from device {DeviceId}.")]
    partial void LogWriteConnectionRequestChannelCompleted(string deviceId);

    [LoggerMessage(EventId = 2075, Level = LogLevel.Error, Message = "WriteConnectionRequest: unexpected error writing connection request for device {DeviceId}.")]
    partial void LogWriteConnectionRequestError(string deviceId, Exception ex);

    [LoggerMessage(EventId = 2076, Level = LogLevel.Error, Message = "ResolveConnectionTcs: unexpected error resolving TCS for peer {PeerId}.")]
    partial void LogResolveConnectionTcsError(string peerId, Exception ex);

    [LoggerMessage(EventId = 2077, Level = LogLevel.Error, Message = "FaultConnectionTcs: unexpected error faulting TCS for peer {PeerId}.")]
    partial void LogFaultConnectionTcsError(string peerId, Exception ex);

    [LoggerMessage(EventId = 2078, Level = LogLevel.Error, Message = "WritePayload: unexpected error writing payload for peer {PeerId}.")]
    partial void LogWritePayloadError(string peerId, Exception ex);

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
