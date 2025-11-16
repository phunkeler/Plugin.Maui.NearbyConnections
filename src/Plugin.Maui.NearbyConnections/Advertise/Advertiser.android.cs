namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        _nearbyConnections.OnConnectionInitiated(endpointId, connectionInfo);
    }

    void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        _nearbyConnections.OnConnectionResult(endpointId, resolution);
    }

    void OnDisconnected(string endpointId)
    {
        _nearbyConnections.OnDisconnected(endpointId);
    }

    Task PlatformStartAdvertising(AdvertisingOptions options)
    {
        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        return _connectionClient.StartAdvertisingAsync(
            options.DisplayName,
            options.ServiceName,
            new AdvertiseCallback(OnConnectionInitiated, OnConnectionResult, OnDisconnected),
            new AndroidAdvertisingOptions.Builder()
                .SetStrategy(options.Strategy)
                .SetConnectionType(options.ConnectionType)
                .SetLowPower(options.UseLowPower)
                .Build());
    }

    void PlatformStopAdvertising()
        => _connectionClient?.StopAdvertising();

    sealed class AdvertiseCallback(
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