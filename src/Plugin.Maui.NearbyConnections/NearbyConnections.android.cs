using Plugin.Maui.NearbyConnections.Device;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Logging;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    #region Discovery

    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        _logger.EndpointFound(endpointId, info.EndpointName);

        var device = new NearbyDevice(endpointId, info.EndpointName);

        if (_discoveredDevices.TryAdd(endpointId, device))
        {
        }

        var evt = new NearbyDeviceFound(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    internal void OnEndpointLost(string endpointId)
    {
        _logger.EndpointLost(endpointId);

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

    #endregion Discovery

    #region Advertising

    internal void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        _logger.ConnectionInitiated(
            endpointId,
            connectionInfo.EndpointName,
            connectionInfo.IsIncomingConnection);

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
        _logger.OnConnectionResult(
            endpointId,
            resolution.Status.StatusCode,
            resolution.Status.StatusMessage ?? string.Empty,
            resolution.Status.IsSuccess);

        // Try to get device from discovered devices, or create a new one
        if (!_discoveredDevices.TryGetValue(endpointId, out var device))
        {
            device = new NearbyDevice(endpointId, string.Empty);
        }

        // If connection was successful, add to connected devices
        if (resolution.Status.IsSuccess)
        {
            if (_connectedDevices.TryAdd(endpointId, device))
            {
            }
        }
        else
        {
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

    #endregion Advertising
}