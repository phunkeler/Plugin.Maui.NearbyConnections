using Plugin.Maui.NearbyConnections.Device;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    #region Discoverer

    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        var device = new NearbyDevice(endpointId, info.EndpointName);

        _discoveredDevices.TryAdd(endpointId, device);

        var evt = new NearbyDeviceFound(
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

        var evt = new NearbyDeviceLost(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    #endregion Discoverer

    #region Advertiser

    internal void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        if (!_discoveredDevices.TryGetValue(endpointId, out var device))
        {
            return;
        }

        // We're missing-out on a lot of details by not passing ConnectionInfo, but this is sufficient for now.
        var evt = new InvitationReceived(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    internal void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        // Try to get device from discovered devices, or create a new one
        if (!_discoveredDevices.TryGetValue(endpointId, out var device))
        {
            device = new NearbyDevice(endpointId, string.Empty);
        }

        // If connection was successful, add to connected devices
        if (resolution.Status.IsSuccess)
        {
            _connectedDevices.TryAdd(endpointId, device);
        }

        var evt = new InvitationAnswered(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    internal void OnDisconnected(string endpointId)
    {
        if (!_connectedDevices.TryRemove(endpointId, out var device))
        {
            return;
        }

        var evt = new NearbyDeviceDisconnected(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    #endregion Advertiser
}