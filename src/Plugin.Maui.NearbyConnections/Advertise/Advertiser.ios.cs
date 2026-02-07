namespace Plugin.Maui.NearbyConnections.Advertise;

sealed partial class Advertiser : NSObject, IMCNearbyServiceAdvertiserDelegate
{
    MCNearbyServiceAdvertiser? _advertiser;

    Task PlatformStartAdvertising(string displayName)
    {
        var options = _nearbyConnections.Options;

        var myPeerId = _nearbyConnections.MyMCPeerIDManager.GetPeerId(displayName)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        // TODO: Future enhancement - support custom advertisement info
        NSDictionary? advertisingInfo = null;

        _advertiser = new MCNearbyServiceAdvertiser(
            myPeerID: myPeerId,
            info: advertisingInfo,
            serviceType: options.ServiceId
        )
        {
            Delegate = this
        };

        _advertiser.StartAdvertisingPeer();
        IsAdvertising = true;

        return Task.CompletedTask;
    }

    void PlatformStopAdvertising()
    {
        _advertiser?.StopAdvertisingPeer();
        _advertiser?.Dispose();
        _advertiser = null;
        IsAdvertising = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                _advertiser?.StopAdvertisingPeer();
                _advertiser?.Dispose();
                _advertiser = null;
                IsAdvertising = false;
            }
        }

        base.Dispose(disposing);
    }

    public void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
        => _nearbyConnections.DidNotStartAdvertisingPeer(advertiser, error);

    public void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
        => _nearbyConnections.DidReceiveInvitationFromPeer(advertiser, peerID, context, invitationHandler);
}