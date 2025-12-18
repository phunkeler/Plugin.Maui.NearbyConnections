namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        _nearbyConnections?.OnEndpointFound(endpointId, info);
    }

    void OnEndpointLost(string endpointId)
    {
        _nearbyConnections?.OnEndpointLost(endpointId);
    }

    Task PlatformStartDiscovering()
    {
        var options = _nearbyConnections.Options;
        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        return _connectionClient.StartDiscoveryAsync(
            options.ServiceName,
            new DiscoveryCallback(OnEndpointFound, OnEndpointLost),
            new DiscoveryOptions.Builder()
                .SetStrategy(options.Strategy)
                .SetLowPower(options.UseLowPower)
                .Build());
    }

    void PlatformStopDiscovering()
        => _connectionClient?.StopDiscovery();

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                _connectionClient?.StopDiscovery();
                _connectionClient?.Dispose();
                _connectionClient = null;
            }
        }

        base.Dispose(disposing);
    }

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