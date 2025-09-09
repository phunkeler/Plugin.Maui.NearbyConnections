using Foundation;
using MultipeerConnectivity;
using Plugin.Maui.NearbyConnections.Device;

namespace Plugin.Maui.NearbyConnections.Events.Adapters;

internal sealed record FoundPeer(MCNearbyServiceBrowser Browser, MCPeerID PeerID, NSDictionary? Info);

internal sealed partial class NearbyDeviceFoundAdapter : IEventAdapter<FoundPeer, NearbyDeviceFound>
{
    public MyPeerIdManager? MyPeerIdManager { get; init; }

    public NearbyDeviceFound? Transform(FoundPeer platformArgs)
    {
        // Generate NearbyDevice Id
        using var serializedPeerId = MyPeerIdManager?.ArchivePeerId(platformArgs.PeerID);
        var bytes = serializedPeerId?.ToArray();
        var id = Convert.ToBase64String(bytes!);


        var device = new NearbyDevice(id, platformArgs.PeerID.DisplayName);
        return new NearbyDeviceFound("", DateTimeOffset.UtcNow, device);
    }
}