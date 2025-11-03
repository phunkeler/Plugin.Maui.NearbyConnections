using Foundation;
using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections.Advertise;

internal partial class Advertiser : NSObject, IMCNearbyServiceAdvertiserDelegate
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    MCNearbyServiceAdvertiser? _advertiser;

    Task PlatformStartAdvertising(AdvertiseOptions options)
    {
        var myPeerId = _myMCPeerIDManager.GetPeerId(options.DisplayName)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        NSDictionary? advertisingInfo = null;
        if (options.AdvertisingInfo?.Any() == true)
        {
            Console.WriteLine($"[ADVERTISER] Adding advertising info with {options.AdvertisingInfo.Count} items");
            advertisingInfo = NSDictionary.FromObjectsAndKeys(
                options.AdvertisingInfo.Values.ToArray(),
                options.AdvertisingInfo.Keys.ToArray()
            );
        }

        _advertiser = new MCNearbyServiceAdvertiser(
            myPeerID: myPeerId,
            info: advertisingInfo,
            serviceType: options.ServiceName
        )
        {
            Delegate = this
        };

        _advertiser.StartAdvertisingPeer();

        return Task.CompletedTask;
    }

    void PlatformStopAdvertising()
        => _advertiser?.StopAdvertisingPeer();

    public void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
        => _nearbyConnections.DidNotStartAdvertisingPeer(advertiser, error);

    public void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
        => _nearbyConnections.DidReceiveInvitationFromPeer(advertiser, peerID, context, invitationHandler);
}