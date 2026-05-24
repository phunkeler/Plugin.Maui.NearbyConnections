namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    // -------------------------------------------------------------------------
    // Advertising
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Debug, Message = "Advertising is already active.")]
    partial void LogAdvertisingAlreadyActive();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Starting advertising: ServiceId={ServiceId}, DisplayName={DisplayName}")]
    partial void LogStartingAdvertising(string serviceId, string displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Advertising started: ServiceId={ServiceId}, DisplayName={DisplayName}")]
    partial void LogAdvertisingStarted(string serviceId, string displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Advertising is not currently active.")]
    partial void LogAdvertisingNotActive();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Stopping advertising: ServiceId={ServiceId}, DisplayName={DisplayName}")]
    partial void LogStoppingAdvertising(string serviceId, string displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Advertising stopped: ServiceId={ServiceId}, DisplayName={DisplayName}")]
    partial void LogAdvertisingStopped(string serviceId, string displayName);

    // -------------------------------------------------------------------------
    // Discovery
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovery is already active.")]
    partial void LogDiscoveryAlreadyActive();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Starting discovery: ServiceId={ServiceId}")]
    partial void LogStartingDiscovery(string serviceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovery started: ServiceId={ServiceId}")]
    partial void LogDiscoveryStarted(string serviceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovery is not currently active.")]
    partial void LogDiscoveryNotActive();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Stopping discovery: ServiceId={ServiceId}")]
    partial void LogStoppingDiscovery(string serviceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovery stopped: ServiceId={ServiceId}")]
    partial void LogDiscoveryStopped(string serviceId);

    // -------------------------------------------------------------------------
    // Devices
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device found: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDeviceFound(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device lost: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDeviceLost(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device disconnected: Id={DeviceId}")]
    partial void LogDeviceDisconnected(string deviceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connected device stopped advertising, connection remains: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectedDeviceStoppedAdvertising(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No peer found for device: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogNoPeerFoundForDevice(string deviceId, string? displayName);

    // -------------------------------------------------------------------------
    // Connections
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending connection request to: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogSendingConnectionRequest(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Responding to connection request from: Id={DeviceId}, DisplayName={DisplayName}, Accept={Accept}")]
    partial void LogRespondingToConnectionRequest(string deviceId, string? displayName, bool accept);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection request received from: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectionRequestReceived(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Auto-accepting connection from: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogAutoAcceptingConnection(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disconnecting from device: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDisconnecting(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send disconnect message to device: Id={DeviceId}, DisplayName={DisplayName}, Error={Error}")]
    partial void LogFailedToSendDisconnect(string deviceId, string? displayName, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invitation expired from: Id={DeviceId}, DisplayName={DisplayName}, Timeout={TimeoutSeconds}s")]
    partial void LogInvitationExpired(string deviceId, string? displayName, double timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot request connection: discovery is not active.")]
    partial void LogRequestConnectionNotDiscovering();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot respond to connection: invitation no longer pending for device: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogRespondToConnectionInvitationGone(string deviceId, string? displayName);

    // -------------------------------------------------------------------------
    // Data transfer
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Trace, Message = "Data received from device: Id={DeviceId}, DisplayName={DisplayName}, PayloadType={PayloadType}")]
    partial void LogIncomingDataReceived(string deviceId, string? displayName, string payloadType);

    // -------------------------------------------------------------------------
    // Android-specific
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection result: EndpointId={EndpointId}, StatusCode={StatusCode}, StatusMessage={StatusMessage}, IsSuccess={IsSuccess}")]
    partial void LogConnectionResult(string endpointId, int statusCode, string statusMessage, bool isSuccess);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Payload received: EndpointId={EndpointId}, PayloadId={PayloadId}, PayloadType={PayloadType}")]
    partial void LogPayloadReceived(string endpointId, long payloadId, int payloadType);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Payload transfer update: EndpointId={EndpointId}, PayloadId={PayloadId}, Status={Status}, TotalBytes={TotalBytes}, BytesTransferred={BytesTransferred}")]
    partial void LogPayloadTransferUpdate(string endpointId, long payloadId, int status, long totalBytes, long bytesTransferred);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot send file: '{Uri}' is not a valid URI. Only 'file://' and 'content://' schemes are supported.")]
    partial void LogInvalidFileUri(string uri);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not resolve display name from content URI: {Error}")]
    partial void LogCouldNotResolveContentUriName(string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to build file payload: {Error}")]
    partial void LogBuildFilePayloadFailed(string error);

    // -------------------------------------------------------------------------
    // iOS-specific
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Error, Message = "Advertising failed to start: {Error}")]
    partial void LogDidNotStartAdvertising(string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Discovery failed to start: {Error}")]
    partial void LogDidNotStartBrowsing(string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send bytes to peer: DisplayName={DisplayName}, Error={Error}")]
    partial void LogSendBytesFailed(string displayName, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File transfer stalled: Id={DeviceId}, DisplayName={DisplayName}, Timeout={TimeoutSeconds}s")]
    partial void LogSendFileTimeout(string deviceId, string? displayName, double timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "File transfer failed: Id={DeviceId}, DisplayName={DisplayName}, Error={Error}")]
    partial void LogSendFileFailed(string deviceId, string? displayName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Last peer disconnected, session disposed.")]
    partial void LogSessionDisposed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Peer state changed: Id={DeviceId}, DisplayName={DisplayName}, State={State}")]
    partial void LogPeerStateChanged(string deviceId, string displayName, string? state);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Data received from peer: Id={DeviceId}, DisplayName={DisplayName}, Length={Length} bytes")]
    partial void LogDataReceived(string deviceId, string displayName, long length);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Control message received from peer: Id={DeviceId}, DisplayName={DisplayName}, Type={Type}")]
    partial void LogControlMessageReceived(string deviceId, string displayName, string? type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping received data from unknown peer: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDroppingDataFromUnknownPeer(string deviceId, string displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disconnecting from session due to control message.")]
    partial void LogDisconnectingFromSession();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown control message type: {Type}")]
    partial void LogUnknownControlMessageType(object type);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Started receiving resource from: Id={DeviceId}, DisplayName={DisplayName}, ResourceName={ResourceName}")]
    partial void LogResourceReceiveStarted(string deviceId, string displayName, string resourceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Finished receiving resource from: Id={DeviceId}, DisplayName={DisplayName}, ResourceName={ResourceName}, Location={Location}, Error={Error}")]
    partial void LogResourceReceiveFinished(string deviceId, string displayName, string resourceName, string? location, string? error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to copy received file: Source={Source}, Destination={Destination}, Error={Error}")]
    partial void LogFileCopyFailed(string source, string destination, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete temporary received file: Path={Path}, Error={Error}")]
    partial void LogFileDeleteFailed(string path, string error);

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disposing NearbyConnections.")]
    partial void LogDisposing();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to acquire {Semaphore} semaphore during dispose.")]
    partial void LogSemaphoreWaitFailed(string semaphore, Exception ex);
}
