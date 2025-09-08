using Plugin.Maui.NearbyConnections.Device;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Discover;

public partial class Discoverer : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    async Task PlatformStartDiscovering(DiscoverOptions options)
    {
        Console.WriteLine($"[DISCOVERER] Starting discovery for service: {options.ServiceName}");

        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        await _connectionClient.StartDiscoveryAsync(
            options.ServiceName,
            new DiscoveryCallback(OnNearbyDeviceFound, OnNearbyDeviceLost),
            new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());

        Console.WriteLine("[DISCOVERER] StartDiscoveryAsync() called successfully");
    }

    void OnNearbyDeviceFound(NearbyDeviceFound nearbyDeviceFound)
    {
        _eventPublisher.Publish(nearbyDeviceFound);
    }

    void OnNearbyDeviceLost(NearbyDeviceLost nearbyDeviceLost)
    {
        _eventPublisher.Publish(nearbyDeviceLost);
    }

    /// <summary>
    /// Stops discovering nearby devices.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    void PlatformStopDiscovering()
    {
        Console.WriteLine("[DISCOVERER] Stopping discovery...");
        _connectionClient?.StopDiscovery();
    }

    sealed class DiscoveryCallback(Action<NearbyDeviceFound> nearbyDeviceFound,
        Action<NearbyDeviceLost> nearbyDeviceLost) : EndpointDiscoveryCallback
    {
        readonly Action<NearbyDeviceFound> _nearbyDeviceFound = nearbyDeviceFound;
        readonly Action<NearbyDeviceLost> _nearbyDeviceLost = nearbyDeviceLost;

        public override void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
        {
            Console.WriteLine($"[DISCOVERER] Endpoint found: {endpointId}, Info: {info}");

            var evt = new NearbyDeviceFound(
                Guid.NewGuid().ToString(),
                DateTimeOffset.UtcNow,
                new NearbyDevice(endpointId, info.EndpointName));

            _nearbyDeviceFound(evt);
        }

        public override void OnEndpointLost(string endpointId)
        {
            Console.WriteLine($"[DISCOVERER] Endpoint lost: {endpointId}");

            // TODO: Get NearbyDevice from discovered list
            var evt = new NearbyDeviceLost(
                Guid.NewGuid().ToString(),
                DateTimeOffset.UtcNow,
                new NearbyDevice(endpointId, ""));

            _nearbyDeviceLost(evt);
        }
    }
}