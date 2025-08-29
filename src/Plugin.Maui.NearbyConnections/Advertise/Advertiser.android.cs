namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
public partial class Advertiser : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    public async Task PlatformStartAdvertising(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ADVERTISER] Starting advertising with name: {options.DisplayName}, service: {options.ServiceName}");

        _connectionClient ??= NearbyClass.GetConnectionsClient(Android.App.Application.Context);

        await _connectionClient.StartAdvertisingAsync(
            options.DisplayName,
            options.ServiceName,
            new AdvertiseCallback(this),
            new Android.Gms.Nearby.Connection.AdvertisingOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());

        Console.WriteLine("[ADVERTISER] StartAdvertisingAsync() called successfully");

    }

    public Task PlatformStopAdvertising(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[ADVERTISER] Stopping advertising...");

        if (_connectionClient is not null)
        {
            _connectionClient.StopAdvertising();
            _connectionClient.Dispose();
            _connectionClient = null;
            Console.WriteLine("[ADVERTISER] Advertising stopped and all endpoints disconnected");
        }
        else
        {
            Console.WriteLine("[ADVERTISER] Connection client is not initialized");
        }

        return Task.CompletedTask;
    }
}

internal sealed class AdvertiseCallback : ConnectionLifecycleCallback
{
    readonly WeakReference<Advertiser> _advertiserRef;

    public AdvertiseCallback(Advertiser advertiser)
    {
        _advertiserRef = new WeakReference<Advertiser>(advertiser);
    }

    public override void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        Console.WriteLine($"[ADVERTISER] Connection initiated with endpoint: {endpointId}");

        if (_advertiserRef.TryGetTarget(out var advertiser))
        {
            advertiser.OnConnectionInitiated(endpointId, connectionInfo.EndpointName);
        }
    }

    public override void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        Console.WriteLine($"[ADVERTISER] Connection result for endpoint: {endpointId}, resolution: {resolution}");

        var success = resolution.Status.StatusCode == ConnectionsStatusCodes.StatusOk;

        if (_advertiserRef.TryGetTarget(out var advertiser))
        {
            advertiser.OnConnectionResult(endpointId, success);
        }
    }

    public override void OnDisconnected(string endpointId)
    {
        Console.WriteLine($"[ADVERTISER] Disconnected from endpoint: {endpointId}");

        if (_advertiserRef.TryGetTarget(out var advertiser))
        {
            advertiser.OnDisconnected(endpointId);
        }
    }
}
