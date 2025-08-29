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

        _connectionClient ??= NearbyClass.GetConnectionsClient(Android.App.Application.Context);

        await _connectionClient.StartDiscoveryAsync(
            options.ServiceName,
            new DiscoveryCallback(),
            new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());

        Console.WriteLine("[DISCOVERER] StartDiscoveryAsync() called successfully");
    }

    /// <summary>
    /// Stops discovering nearby devices.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    private Task PlatformStopDiscovering(CancellationToken cancellationToken)
    {
        Console.WriteLine("[DISCOVERER] Stopping discovery...");

        if (_connectionClient is null)
        {
            Console.WriteLine("[DISCOVERER] ERROR: Connection client is not initialized");
            return Task.CompletedTask;
        }

        _connectionClient.StopDiscovery();

        return Task.CompletedTask;
    }
}

sealed internal class DiscoveryCallback : EndpointDiscoveryCallback
{
    public override void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        Console.WriteLine($"[DISCOVERER] Endpoint found: {endpointId}, Info: {info}");
        // Handle endpoint discovery logic here
    }

    public override void OnEndpointLost(string endpointId)
    {
        Console.WriteLine($"[DISCOVERER] Endpoint lost: {endpointId}");
        // Handle endpoint loss logic here
    }
}