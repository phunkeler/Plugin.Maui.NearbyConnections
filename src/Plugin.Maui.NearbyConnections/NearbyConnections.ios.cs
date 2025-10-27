using Foundation;
using MultipeerConnectivity;
using Plugin.Maui.NearbyConnections.Device;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = new NearbyDevice(id, peerID.DisplayName);

        _discoveredDevices.TryAdd(id, device);

        var evt = new Events.NearbyDeviceFound(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        if (!_discoveredDevices.TryRemove(id, out var device))
        {
            return;
        }

        var evt = new Events.NearbyDeviceLost(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            device);

        ProcessEvent(evt);
    }
}