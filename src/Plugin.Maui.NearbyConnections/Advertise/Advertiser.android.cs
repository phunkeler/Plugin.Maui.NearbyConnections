namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
public partial class Advertiser : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    public async Task PlatformStartAdvertising(IAdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ADVERTISER] Starting advertising with name: {options.DisplayName}, service: {options.ServiceName}");

        _connectionClient ??= NearbyClass.GetConnectionsClient(Android.App.Application.Context);

        await _connectionClient.StartAdvertisingAsync(
            options.DisplayName,
            options.ServiceName,
            new AdvertiseCallback(),
            new AdvertisingOptions.Builder().SetStrategy(Android.Gms.Nearby.Connection.Strategy.P2pPointToPoint).Build());

        Console.WriteLine("[ADVERTISER] StartAdvertisingAsync() called successfully");

    }

    public Task PlatformStopAdvertising(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[ADVERTISER] Stopping advertising...");

        if (_connectionClient is null)
        {
            Console.WriteLine("[ADVERTISER] ERROR: Connection client is not initialized");
            return Task.CompletedTask;
        }

        _connectionClient.StopAdvertising();

        return Task.CompletedTask;
    }
}

sealed internal class AdvertiseCallback : ConnectionLifecycleCallback
{
    public override void OnConnectionInitiated(string p0, ConnectionInfo p1)
    {
        Console.WriteLine($"[ADVERTISER] Connection initiated with endpoint: {p0}");
        // Handle connection initiation logic here
    }

    // Is this method called when a nearby device is "discovered"?
    public override void OnConnectionResult(string p0, ConnectionResolution p1)
    {
        Console.WriteLine($"[ADVERTISER] Connection result for endpoint: {p0}, resolution: {p1}");
        // Handle connection result logic here
        // NearbyConnectionsEventHandler -- Write out in a sample app how you'd like consumers to handle connection results (Cast to our object HERE. )
    }

    public override void OnDisconnected(string p0)
    {
        Console.WriteLine($"[ADVERTISER] Disconnected from endpoint: {p0}");
        // Handle disconnection logic here
    }
}
