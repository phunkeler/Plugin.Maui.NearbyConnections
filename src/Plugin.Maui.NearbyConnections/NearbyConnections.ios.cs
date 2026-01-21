namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
    internal MyPeerIdManager MyMCPeerIDManager { get; } = new();

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Trace.WriteLine($"Found peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Trace.WriteLine($"Lost peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnDeviceLost(device, TimeProvider.GetUtcNow());
    }

    #endregion Discovery

    #region Advertising

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        _advertiser?.OnAdvertisingFailed();

        Events.OnError(
            "Advertising",
            error.LocalizedDescription,
            TimeProvider.GetUtcNow());
    }

    internal void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        Trace.WriteLine($"Received invitation from peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnConnectionRequested(device, TimeProvider.GetUtcNow());
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