using Plugin.Maui.NearbyConnections.Device;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

public partial class Advertiser : Java.Lang.Object
{
    IConnectionsClient? _connectionClient;

    public Task PlatformStartAdvertising(AdvertiseOptions options)
    {
        _connectionClient ??= NearbyClass.GetConnectionsClient(options.Activity ?? Android.App.Application.Context);

        return _connectionClient.StartAdvertisingAsync(
            options.DisplayName,
            options.ServiceName,
            new AdvertiseCallback(x => { }, y => { }),
            new AdvertisingOptions.Builder().SetStrategy(Strategy.P2pCluster).Build());
    }

    public void PlatformStopAdvertising()
        => _connectionClient?.StopAdvertising();
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
    }
}
