using Plugin.Maui.NearbyConnections.Device;
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
            new AdvertiseCallback(x => { }, y => { }),
            new AdvertisingOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());

        Console.WriteLine("[ADVERTISER] StartAdvertisingAsync() called successfully");

    }

    public Task PlatformStopAdvertising()
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
    readonly Action<InvitationAnswered> _invitationAnswered;
    readonly Action<InvitationReceived> _invitationReceived;

    public AdvertiseCallback(
        Action<InvitationAnswered> invitationAnswered,
        Action<InvitationReceived> invitationReceived)
    {
        _invitationAnswered = invitationAnswered;
        _invitationReceived = invitationReceived;
    }

    public override void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        Console.WriteLine($"[ADVERTISER] Connection initiated with endpoint: {endpointId}");

        var invitationReceived = new InvitationReceived
        (
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            new NearbyDevice(endpointId, connectionInfo.EndpointName)
        );

        _invitationReceived(invitationReceived);
    }

    public override void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        Console.WriteLine($"[ADVERTISER] Connection result for endpoint: {endpointId}, resolution: {resolution}");

        if (!resolution.Status.IsSuccess)
        {
            return;
        }

        // Get INearbyDevice from "Pending" registry
        var device = new NearbyDevice(endpointId, "");

        var invitationAnswered = new InvitationAnswered
        (
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device
        );

        _invitationAnswered(invitationAnswered);
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
