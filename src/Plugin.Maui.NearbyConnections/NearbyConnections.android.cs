using Plugin.Maui.NearbyConnections.Device;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        var device = new NearbyDevice(endpointId, info.EndpointName);

        _discoveredDevices.TryAdd(endpointId, device);

        var evt = new Events.NearbyDeviceFound(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    internal void OnEndpointLost(string endpointId)
    {
        if (!_discoveredDevices.TryRemove(endpointId, out var device))
        {
            return;
        }

        var evt = new Events.NearbyDeviceLost(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }
}