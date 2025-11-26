namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var advertisement = NearbyAdvertisement.FromNSDictionary(info);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Trace.WriteLine($"Found peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnDeviceFound(device, _timeProvider.GetUtcNow());
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Trace.WriteLine($"Lost peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnDeviceLost(device, _timeProvider.GetUtcNow());
    }

    #endregion Discovery

    #region Advertising

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        Events.OnError(
            "Advertising",
            error.LocalizedDescription,
            _timeProvider.GetUtcNow());
    }

    internal void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Trace.WriteLine($"Received invitation from peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnConnectionRequested(device, _timeProvider.GetUtcNow());
    }

    #endregion Advertising

    Task PlatformRequestConnectionAsync(NearbyDevice device)
    {
        throw new NotImplementedException();
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        throw new NotImplementedException();
    }
}