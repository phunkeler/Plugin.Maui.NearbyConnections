namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnections
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        _logger.FoundPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var advertisement = NearbyAdvertisement.FromNSDictionary(info);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Events.OnDeviceFound(device, _timeProvider.GetUtcNow());
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        _logger.LostPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Events.OnDeviceLost(device, _timeProvider.GetUtcNow());
    }

    #endregion Discovery

    #region Advertising

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        _logger.DidNotStartAdvertisingPeer(
            advertiser.ServiceType,
            advertiser.MyPeerID.DisplayName,
            error.LocalizedDescription);
    }

    internal void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        _logger.DidReceiveInvitationFromPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Events.OnConnectionRequested(device, _timeProvider.GetUtcNow());
    }

    #endregion Advertising

    Task PlatformSendInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    Task PlatformAcceptInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    Task PlatformDeclineInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}