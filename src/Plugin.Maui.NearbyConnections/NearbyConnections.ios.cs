using Foundation;
using MultipeerConnectivity;
using Plugin.Maui.NearbyConnections.Device;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Logging;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        _logger.FoundPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        _discoveredDevices.TryAdd(id, device);

        var evt = new NearbyDeviceFound(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        _logger.LostPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        if (!_discoveredDevices.TryRemove(id, out var device))
        {
            return;
        }

        var evt = new NearbyDeviceLost(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
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

        // We're missing-out on a lot of details by not passing ConnectionInfo, but this is sufficient for now.
        var evt = new InvitationReceived(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    #endregion Advertising
}