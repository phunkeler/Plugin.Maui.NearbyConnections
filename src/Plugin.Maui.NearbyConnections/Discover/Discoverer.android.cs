using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Discover;

public partial class Discoverer : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    /// <summary>
    /// Starts discovering nearby devices.
    /// </summary>
    /// <param name="options">
    /// Options that modify discovery behavior.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    private async Task PlatformStartDiscovering(DiscoverOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DISCOVERER] Starting discovery for service: {options.ServiceName}");

        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        await _connectionClient.StartDiscoveryAsync(
            options.ServiceName,
            new DiscoveryCallback(new WeakReference<INearbyConnectionsEventProducer>(_eventProducer)),
            new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());

        Console.WriteLine("[DISCOVERER] StartDiscoveryAsync() called successfully");
    }

    /// <summary>
    /// Stops discovering nearby devices.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    private void PlatformStopDiscovering()
    {
        Console.WriteLine("[DISCOVERER] Stopping discovery...");
        _connectionClient?.StopDiscovery();
    }

    sealed class DiscoveryCallback(WeakReference<INearbyConnectionsEventProducer> eventProducerRef) : EndpointDiscoveryCallback
    {
        public override void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
        {
            Console.WriteLine($"[DISCOVERER] Endpoint found: {endpointId}, Info: {info}");

            if (eventProducerRef.TryGetTarget(out var eventProducer))
            {
                eventProducer.PublishAsync(new NearbyConnectionFound(TimeProvider.System, endpointId, info.EndpointName));
            }
        }

        public override void OnEndpointLost(string endpointId)
        {
            Console.WriteLine($"[DISCOVERER] Endpoint lost: {endpointId}");

            if (eventProducerRef.TryGetTarget(out var eventProducer))
            {
                eventProducer.PublishAsync(new NearbyConnectionLost(TimeProvider.System, endpointId));
            }
        }
    }
}