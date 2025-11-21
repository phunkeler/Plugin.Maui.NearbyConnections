namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser : NSObject, IMCNearbyServiceAdvertiserDelegate
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    MCNearbyServiceAdvertiser? _advertiser;

    Task PlatformStartAdvertising(string displayName)
    {
        var options = _nearbyConnections._options;

        var myPeerId = _myMCPeerIDManager.GetPeerId(displayName)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        // TODO: Future enhancement - support custom advertisement info
        NSDictionary? advertisingInfo = null;

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