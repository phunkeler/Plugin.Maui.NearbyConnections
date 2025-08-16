namespace Plugin.Maui.NearbyConnections;

public partial class NearbyConnectionsDiscoverer : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    private async Task PlatformStartDiscovering(IDiscoveringOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DISCOVERER] Starting discovery for service: {options.ServiceName}");

        _connectionClient ??= NearbyClass.GetConnectionsClient(Android.App.Application.Context);

        await _connectionClient.StartDiscoveryAsync(
            options.ServiceName,
            new DiscoveryCallback(),
            new DiscoveryOptions.Builder().SetStrategy(Android.Gms.Nearby.Connection.Strategy.P2pPointToPoint).Build());

        Console.WriteLine("[DISCOVERER] StartDiscoveryAsync() called successfully");
    }

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