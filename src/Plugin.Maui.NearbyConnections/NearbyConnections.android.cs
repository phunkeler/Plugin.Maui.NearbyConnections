namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    #region Discovery

    public void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        Trace.WriteLine($"Endpoint found: EndpointId={endpointId}, EndpointName={info.EndpointName}");
        var device = _deviceManager.DeviceFound(endpointId, info.EndpointName);
        Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    public void OnEndpointLost(string endpointId)
    {
        Trace.WriteLine($"Endpoint lost: EndpointId={endpointId}");
        var device = _deviceManager.DeviceLost(endpointId);
        if (device is not null)
        {
            Events.OnDeviceLost(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Discovery

    #region Advertising

    /// <summary>
    /// "A basic encrypted channel has been created between you and the endpoint.
    /// Both sides are now asked if they wish to accept or reject the connection before any data can be sent over this channel."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectioninitiated-string-endpointid,-connectioninfo-connectioninfo">developers.google.com</see>
    /// </summary>
    /// <param name="endpointId">The identifier for the remote endpoint.</param>
    /// <param name="connectionInfo">Other relevant information about the connection.</param>
    public async void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        Trace.WriteLine($"Connection initiated: EndpointId={endpointId}, EndpointName={connectionInfo.EndpointName}, IsIncomingConnection={connectionInfo.IsIncomingConnection}");

        var state = connectionInfo.IsIncomingConnection
            ? NearbyDeviceState.ConnectionRequestedInbound
            : NearbyDeviceState.ConnectionRequestedOutbound;

        var device = _deviceManager.SetState(endpointId, state)
            ?? _deviceManager.GetOrAddDevice(endpointId, connectionInfo.EndpointName, state);

        if (connectionInfo.IsIncomingConnection)
        {
            Events.OnConnectionRequested(device, TimeProvider.GetUtcNow());

            if (Options.AutoAcceptConnections)
            {
                await PlatformRespondToConnectionAsync(device, accept: true);
            }
        }
        else
        {
            // Skip this extra step. The discoverer already initiated the connection request.
            // TODO: Consider exposing this extra step as a "security" option and optionally leverages the 4 Digit Auth Token in ConnectionInfo.
            await PlatformRespondToConnectionAsync(device, accept: true);
        }
    }

    /// <summary>
    /// "Called after both sides have either accepted or rejected the connection.
    /// If the ConnectionResolution's status is CommonStatusCodes.SUCCESS, both sides have
    /// accepted the connection and may now send Payloads to each other. Otherwise, the connection was rejected."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectionresult-string-endpointid,-connectionresolution-resolution">developers.google.com</see>
    /// </summary>
    /// <param name="endpointId">The identifier for the remote endpoint</param>
    /// <param name="resolution">The final result after tallying both devices' accept/reject responses</param>
    public void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        Trace.WriteLine($"Connection result: EndpointId={endpointId}, StatusCode={resolution.Status.StatusCode}, StatusMessage={resolution.Status.StatusMessage ?? string.Empty}, IsSuccess={resolution.Status.IsSuccess}");

        if (resolution.Status.IsSuccess)
        {
            var device = _deviceManager.SetState(endpointId, NearbyDeviceState.Connected);

            if (device is not null)
            {
                Events.OnConnectionResponded(device, TimeProvider.GetUtcNow(), true);
            }
        }
        else
        {
            var device = _deviceManager.SetState(endpointId, NearbyDeviceState.Discovered);

            if (device is not null)
            {
                Events.OnConnectionResponded(device, TimeProvider.GetUtcNow(), false);
            }
        }
    }

    /// <summary>
    /// "Called when a remote endpoint is disconnected or has become unreachable."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-ondisconnected-string-endpointid">developers.google.com</see>
    /// </summary>
    /// <param name="endpointId"></param>
    public void OnDisconnected(string endpointId)
    {
        Trace.WriteLine($"Disconnected from EndpointId={endpointId}");

        var device = _deviceManager.DeviceDisconnected(endpointId);

        if (device is not null)
        {
            Events.OnDeviceDisconnected(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Advertising

    static void OnPayloadReceived(string endpointId, Payload payload)
    {
        Trace.WriteLine($"Payload received: EndpointId={endpointId}, PayloadId={payload.Id}, PayloadType={payload.PayloadType}");
    }

    static void OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        Trace.WriteLine($"Payload transfer update: EndpointId={endpointId}, PayloadId={update.PayloadId}, TransferStatus={update.TransferStatus}, TotalBytes={update.TotalBytes}, BytesTransferred={update.BytesTransferred}");
    }

    Task PlatformRequestConnectionAsync(NearbyDevice device)
    {
        _deviceManager.SetState(device.Id, NearbyDeviceState.ConnectionRequestedOutbound);

        return NearbyClass
            .GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
            .RequestConnectionAsync(
                DisplayName,
                device.Id,
                new ConnectionRequestCallback(
                    OnConnectionInitiated,
                    OnConnectionResult,
                    OnDisconnected));
    }

    static Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return accept
            ? client.AcceptConnectionAsync(device.Id, new ConnectionCallback(OnPayloadReceived, OnPayloadTransferUpdate))
            : client.RejectConnectionAsync(device.Id);
    }

    sealed class ConnectionRequestCallback(
        Action<string, ConnectionInfo> onConnectionInitiated,
        Action<string, ConnectionResolution> onConnectionResult,
        Action<string> onDisconnected) : ConnectionLifecycleCallback
    {
        public override void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
            => onConnectionInitiated(endpointId, connectionInfo);

        public override void OnConnectionResult(string endpointId, ConnectionResolution resolution)
            => onConnectionResult(endpointId, resolution);

        public override void OnDisconnected(string endpointId)
            => onDisconnected(endpointId);
    }

    sealed class ConnectionCallback(
        Action<string, Payload> onPayloadReceived,
        Action<string, PayloadTransferUpdate> onPayloadTransferUpdate) : PayloadCallback
    {
        public override void OnPayloadReceived(string p0, Payload p1)
            => onPayloadReceived(p0, p1);

        public override void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            => onPayloadTransferUpdate(p0, p1);
    }
}