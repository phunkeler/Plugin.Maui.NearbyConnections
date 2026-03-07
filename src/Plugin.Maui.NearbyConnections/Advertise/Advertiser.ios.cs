namespace Plugin.Maui.NearbyConnections.Advertise;

sealed partial class Advertiser : NSObject, IMCNearbyServiceAdvertiserDelegate
{
    MCNearbyServiceAdvertiser? _advertiser;

    Task PlatformStartAdvertising()
    {
        var options = _nearbyConnections.Options;

        var myPeerId = PeerIdManager.GetLocalPeerId(options.DisplayName)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        _advertiser = new MCNearbyServiceAdvertiser(
            myPeerID: myPeerId,
            info: null,
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