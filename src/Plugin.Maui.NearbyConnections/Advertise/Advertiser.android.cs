using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
public partial class Advertiser : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    public async Task PlatformStartAdvertising(AdvertiseOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ADVERTISER] Starting advertising with name: {options.DisplayName}, service: {options.ServiceName}");

        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        await _connectionClient.StartAdvertisingAsync(
            options.DisplayName,
            options.ServiceName,
            new AdvertiseCallback(_eventProducer),
            new AdvertisingOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());

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

    /// <summary>
    /// Disposes of resources used by the advertiser.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_connectionClient is not null)
            {
                _connectionClient.StopAdvertising();
                _connectionClient.StopAllEndpoints();
                _connectionClient.Dispose();
                _connectionClient = null;
            }

            _connectionClient?.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class AdvertiseCallback : ConnectionLifecycleCallback
{
    readonly INearbyConnectionsEventProducer _eventProducer;

    public AdvertiseCallback(INearbyConnectionsEventProducer eventProducer)
    {
        _eventProducer = eventProducer;
    }

    public override void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        Console.WriteLine($"[ADVERTISER] Connection initiated with endpoint: {endpointId}");

        _eventProducer.PublishAsync(new InvitationReceived
        {
            ConnectionEndpoint = endpointId,
            InvitingPeer = new Models.PeerDevice
            {
                Id = "",
                DisplayName = connectionInfo.EndpointName,
            },
        });
    }

    // NOT USING YET
    public override void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        Console.WriteLine($"[ADVERTISER] Connection result for endpoint: {endpointId}, resolution: {resolution}");

        /* TODO
                _eventProducer.PublishAsync([?]
                {
                    ConnectionEndpoint = "",
                    InvitingPeer = new Models.PeerDevice
                    {
                        Id = "",
                        DisplayName = "",
                    },
                });
        */
    }

    public override void OnDisconnected(string endpointId)
    {
        Console.WriteLine($"[ADVERTISER] Disconnected from endpoint: {endpointId}");

        /* TODO
                _eventProducer.PublishAsync([?]
                {
                    ConnectionEndpoint = "",
                    InvitingPeer = new Models.PeerDevice
                    {
                        Id = "",
                        DisplayName = "",
                    },
                });
        */
    }
}
