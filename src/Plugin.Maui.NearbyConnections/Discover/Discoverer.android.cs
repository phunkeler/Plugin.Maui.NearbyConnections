namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        _nearbyConnections.OnEndpointFound(endpointId, info);
    }

    void OnEndpointLost(string endpointId)
    {
        _nearbyConnections.OnEndpointLost(endpointId);
    }

    Task PlatformStartDiscovering(DiscoverOptions options)
    {
        // Android warns about NFC needing Activity context
        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        return _connectionClient.StartDiscoveryAsync(
            options.ServiceName,
            new DiscoveryCallback(OnEndpointFound, OnEndpointLost),
            new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());
    }

    void PlatformStopDiscovering()
        => _connectionClient?.StopDiscovery();

    sealed class DiscoveryCallback(
        Action<string, DiscoveredEndpointInfo> onEndpointFound,
        Action<string> onEndpointLost) : EndpointDiscoveryCallback
    {
        readonly Action<string, DiscoveredEndpointInfo> _onEndpointFound = onEndpointFound;
        readonly Action<string> _onEndpointLost = onEndpointLost;

        public override void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
            => _onEndpointFound(endpointId, info);

        public override void OnEndpointLost(string endpointId)
            => _onEndpointLost(endpointId);
    }
}