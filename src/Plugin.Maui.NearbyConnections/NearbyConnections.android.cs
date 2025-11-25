using System.Diagnostics;

namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnections
{
    #region Discovery

    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        _logger.EndpointFound(endpointId, info.EndpointName);
        var device = new NearbyDevice(endpointId, info.EndpointName);
        Events.OnDeviceFound(device, _timeProvider.GetUtcNow());
    }

    internal void OnEndpointLost(string endpointId)
    {
        _logger.EndpointLost(endpointId);
        var device = new NearbyDevice(endpointId, string.Empty);
        Events.OnDeviceLost(device, _timeProvider.GetUtcNow());
    }

    #endregion Discovery

    #region Advertising

    internal void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        _logger.ConnectionInitiated(
            endpointId,
            connectionInfo.EndpointName,
            connectionInfo.IsIncomingConnection);

        var device = new NearbyDevice(endpointId, connectionInfo.EndpointName);

        Events.OnConnectionRequested(device, _timeProvider.GetUtcNow());
    }

    internal void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        _logger.OnConnectionResult(
            endpointId,
            resolution.Status.StatusCode,
            resolution.Status.StatusMessage ?? string.Empty,
            resolution.Status.IsSuccess);

        var device = new NearbyDevice(endpointId, string.Empty);

        Events.OnConnectionResponded(device, _timeProvider.GetUtcNow());
    }

    internal void OnDisconnected(string endpointId)
    {
        _logger.Disconnected(endpointId);

        var device = new NearbyDevice(endpointId, string.Empty);

        Events.OnDeviceDisconnected(device, _timeProvider.GetUtcNow());
    }

    #endregion Advertising

    Task PlatformSendInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        return NearbyClass.GetConnectionsClient(_options.Activity ?? Android.App.Application.Context)
            .RequestConnectionAsync(DisplayName,
            device.Id,
            new InvitationCallback(OnConnectionInitiated, OnConnectionResult, OnDisconnected));
    }

    static Task PlatformAcceptInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        return NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Android.App.Application.Context)
            .AcceptConnectionAsync(device.Id, new MessengerCallback((deviceId, payload) =>
            {
                Debug.WriteLine($"Received payload from EndpointId={deviceId}");
            },
            (deviceId, update) =>
            {
                Debug.WriteLine($"Received payload transfer update from EndpointId={deviceId}");
            }));
    }

    static Task PlatformDeclineInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        return NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Android.App.Application.Context)
            .RejectConnectionAsync(device.Id);
    }


    sealed class InvitationCallback(
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

    sealed class MessengerCallback(
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