namespace Plugin.Maui.NearbyConnections.Discover;

sealed partial class Discoverer : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

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
                IsDiscovering = false;
            }
        }

        base.Dispose(disposing);
    }

    void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
        => _nearbyConnections.OnEndpointFound(endpointId, info);

    void OnEndpointLost(string endpointId)
        => _nearbyConnections?.OnEndpointLost(endpointId);

    async Task PlatformStartDiscovering()
    {
        var options = _nearbyConnections.Options;
        _connectionClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        await _connectionClient.StartDiscoveryAsync(
            options.ServiceId,
            new DiscoveryCallback(OnEndpointFound, OnEndpointLost),
            new DiscoveryOptions.Builder()
                .SetStrategy(options.Strategy)
                .SetLowPower(options.UseLowPower)
                .Build());

        IsDiscovering = true;
    }

    void PlatformStopDiscovering()
    {
        _connectionClient?.StopDiscovery();
        IsDiscovering = false;
    }

    sealed class DiscoveryCallback(
        Action<string, DiscoveredEndpointInfo> onEndpointFound,
        Action<string> onEndpointLost) : EndpointDiscoveryCallback
    {
        public override void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
            => onEndpointFound(endpointId, info);

        public override void OnEndpointLost(string endpointId)
            => onEndpointLost(endpointId);
    }
}