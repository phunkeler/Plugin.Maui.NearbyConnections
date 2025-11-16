namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    #region Discovery

    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        _logger.EndpointFound(endpointId, info.EndpointName);

        var device = new NearbyDevice(endpointId, info.EndpointName, NearbyDeviceStatus.Discovered);

        if (_devices.TryAdd(endpointId, device))
        {
            var evt = new NearbyDeviceFound(
                Guid.NewGuid().ToString(),
                DateTimeOffset.UtcNow,
                device);

            ProcessEvent(evt);

        }
    }

    internal void OnEndpointLost(string endpointId)
    {
        _logger.EndpointLost(endpointId);

        if (_devices.TryGetValue(endpointId, out var device))
        {
            device.Status = NearbyDeviceStatus.Disconnected;
        }
        else
        {
            device = new NearbyDevice(
                endpointId,
                string.Empty,
                NearbyDeviceStatus.Disconnected);

            _devices.TryAdd(endpointId, device);
        }

        var evt = new NearbyDeviceLost(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    #endregion Discovery

    #region Advertising

    internal void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        _logger.ConnectionInitiated(
            endpointId,
            connectionInfo.EndpointName,
            connectionInfo.IsIncomingConnection);

        if (!_devices.TryGetValue(endpointId, out var device))
        {
            device = new NearbyDevice(
                endpointId,
                connectionInfo.EndpointName,
                NearbyDeviceStatus.Invited);

            _devices.TryAdd(endpointId, device);
        }
        else
        {
            device.Status = NearbyDeviceStatus.Invited;
        }

        if (connectionInfo.IsIncomingConnection)
        {
            var evt = new InvitationReceived(
                Guid.NewGuid().ToString(),
                DateTimeOffset.UtcNow,
                device);

            ProcessEvent(evt);
        }
    }

    internal void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        _logger.OnConnectionResult(
            endpointId,
            resolution.Status.StatusCode,
            resolution.Status.StatusMessage ?? string.Empty,
            resolution.Status.IsSuccess);

        if (!_devices.TryGetValue(endpointId, out var device))
        {
            device = new NearbyDevice(
                endpointId,
                string.Empty,
                NearbyDeviceStatus.Invited);

            _devices.TryAdd(endpointId, device);
        }

        if (resolution.Status.IsSuccess)
        {
            device.Status = NearbyDeviceStatus.Connected;
        }
        else
        {
            device.Status = NearbyDeviceStatus.Disconnected;
        }

        var evt = new InvitationAnswered(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    internal void OnDisconnected(string endpointId)
    {
        _logger.Disconnected(endpointId);

        if (_devices.TryGetValue(endpointId, out var device))
        {
            device.Status = NearbyDeviceStatus.Disconnected;
        }
        else
        {
            device = new NearbyDevice(
                endpointId,
                string.Empty,
                NearbyDeviceStatus.Disconnected);

            _devices.TryAdd(endpointId, device);
        }

        var evt = new NearbyDeviceDisconnected(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    #endregion Advertising


    Task PlatformSendInvitation(INearbyDevice device, CancellationToken cancellationToken = default)
    {
        return NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Android.App.Application.Context)
            .RequestConnectionAsync("ME", device.Id, new InvitationCallback(OnConnectionInitiated, OnConnectionResult, OnDisconnected));
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
}