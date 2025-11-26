namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
    #region Discovery

    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        Trace.WriteLine($"Endpoint found: EndpointId={endpointId}, EndpointName={info.EndpointName}");
        var device = new NearbyDevice(endpointId, info.EndpointName);
        Events.OnDeviceFound(device, _timeProvider.GetUtcNow());
    }

    internal void OnEndpointLost(string endpointId)
    {
        Trace.WriteLine($"Endpoint lost: EndpointId={endpointId}");
        var device = new NearbyDevice(endpointId);
        Events.OnDeviceLost(device, _timeProvider.GetUtcNow());
    }

    #endregion Discovery

    #region Advertising

    internal void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        Trace.WriteLine($"Connection initiated: EndpointId={endpointId}, EndpointName={connectionInfo.EndpointName}, IsIncomingConnection={connectionInfo.IsIncomingConnection}");

        var device = new NearbyDevice(endpointId, connectionInfo.EndpointName);
        Events.OnConnectionRequested(device, _timeProvider.GetUtcNow());
    }

    internal void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        Trace.WriteLine($"Connection result: EndpointId={endpointId}, StatusCode={resolution.Status.StatusCode}, StatusMessage={resolution.Status.StatusMessage ?? string.Empty}, IsSuccess={resolution.Status.IsSuccess}");

        var device = new NearbyDevice(endpointId);
        Events.OnConnectionResponded(device, _timeProvider.GetUtcNow());
    }

    internal void OnDisconnected(string endpointId)
    {
        Trace.WriteLine($"Disconnected from EndpointId={endpointId}");

        var device = new NearbyDevice(endpointId);
        Events.OnDeviceDisconnected(device, _timeProvider.GetUtcNow());
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
        => NearbyClass
            .GetConnectionsClient(Options.Activity ?? Android.App.Application.Context)
            .RequestConnectionAsync(
                DisplayName,
                device.Id,
                new ConnectionRequestCallback(
                    OnConnectionInitiated,
                    OnConnectionResult,
                    OnDisconnected));

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        var client = NearbyClass.GetConnectionsClient(Options.Activity ?? Android.App.Application.Context);

        return accept
            ? client.AcceptConnectionAsync(device.Id, new ConnectionCallback(OnPayloadReceived, OnPayloadTransferUpdate))
            : client.RejectConnectionAsync(device.Id);
    }

    sealed class ConnectionRequestCallback(
        Action<string, ConnectionInfo> onConnectionInitiated,
        Action<string, ConnectionResolution> onConnectionResult,
        Action<string> onDisconnected) : ConnectionLifecycleCallback
    {
        readonly Action<string, ConnectionInfo> _onConnectionInitiated = onConnectionInitiated;
        readonly Action<string, ConnectionResolution> _onConnectionResult = onConnectionResult;
        readonly Action<string> _onDisconnected = onDisconnected;

        public override void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
            => _onConnectionInitiated(endpointId, connectionInfo);

        public override void OnConnectionResult(string endpointId, ConnectionResolution resolution)
            => _onConnectionResult(endpointId, resolution);

        public override void OnDisconnected(string endpointId)
            => _onDisconnected(endpointId);
    }

    sealed class ConnectionCallback(
        Action<string, Payload> onPayloadReceived,
        Action<string, PayloadTransferUpdate> onPayloadTransferUpdate) : PayloadCallback
    {
        readonly Action<string, Payload> _onPayloadReceived = onPayloadReceived;
        readonly Action<string, PayloadTransferUpdate> _onPayloadTransferUpdate = onPayloadTransferUpdate;

        public override void OnPayloadReceived(string p0, Payload p1)
            => _onPayloadReceived(p0, p1);

        public override void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            => _onPayloadTransferUpdate(p0, p1);
    }
}