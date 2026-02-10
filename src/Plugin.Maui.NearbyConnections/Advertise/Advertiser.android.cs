namespace Plugin.Maui.NearbyConnections.Advertise;

sealed partial class Advertiser : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                _connectionClient?.StopAdvertising();
                _connectionClient?.Dispose();
                _connectionClient = null;
                IsAdvertising = false;
            }
        }

        base.Dispose(disposing);
    }

    void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
        => _nearbyConnections.OnConnectionInitiated(endpointId, connectionInfo);

    void OnConnectionResult(string endpointId, ConnectionResolution resolution)
        => _nearbyConnections.OnConnectionResult(endpointId, resolution);

    void OnDisconnected(string endpointId)
        => _nearbyConnections.OnDisconnected(endpointId);

    async Task PlatformStartAdvertising()
    {
        var options = _nearbyConnections.Options;

        _connectionClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        await _connectionClient.StartAdvertisingAsync(
            options.DisplayName,
            options.ServiceId,
            new AdvertiseCallback(OnConnectionInitiated, OnConnectionResult, OnDisconnected),
            new AdvertisingOptions.Builder()
                .SetStrategy(options.Strategy)
                .SetConnectionType(options.ConnectionType)
                .SetLowPower(options.UseLowPower)
                .Build());

        IsAdvertising = true;
    }

    void PlatformStopAdvertising()
    {
        _connectionClient?.StopAdvertising();
        IsAdvertising = false;
    }

    sealed class AdvertiseCallback(
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
}