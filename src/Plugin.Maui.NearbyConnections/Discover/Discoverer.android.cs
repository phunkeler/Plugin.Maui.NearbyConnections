using Plugin.Maui.NearbyConnections.Device;

namespace Plugin.Maui.NearbyConnections.Discover;

public partial class Discoverer : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    void OnNearbyDeviceFound(string endpointId, DiscoveredEndpointInfo info)
    {
        var device = new NearbyDevice(endpointId, info.EndpointName);
        _discoveredDevices.TryAdd(endpointId, device);
    }

    void OnNearbyDeviceLost(string endpointId)
    {
        if (!_discoveredDevices.TryRemove(endpointId, out var nearbyDeviceLost))
            return;

        Console.WriteLine($"[DISCOVERER] Lost device: {nearbyDeviceLost}");
    }

    Task PlatformStartDiscovering(DiscoverOptions options)
    {
        // Android warns about NFC needing Activity context
        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        return _connectionClient.StartDiscoveryAsync(
            options.ServiceName,
            new DiscoveryCallback(OnNearbyDeviceFound, OnNearbyDeviceLost),
            new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());
    }

    void PlatformStopDiscovering()
        => _connectionClient?.StopDiscovery();

    sealed class DiscoveryCallback(
        Action<string, DiscoveredEndpointInfo> onEndpointFound,
        Action<string> nearbyDeviceLost) : EndpointDiscoveryCallback
    {
        readonly Action<string, DiscoveredEndpointInfo> _onEndpointFound = onEndpointFound;
        readonly Action<string> _nearbyDeviceLost = nearbyDeviceLost;

        public override void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
        {
            _onEndpointFound(endpointId, info);
        }

        public override void OnEndpointLost(string endpointId)
        {
            _nearbyDeviceLost(endpointId);
        }
    }
}