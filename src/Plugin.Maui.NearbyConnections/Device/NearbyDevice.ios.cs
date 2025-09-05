using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections.Device;

public sealed partial class NearbyDevice
{
    readonly MCPeerID _mCPeerID;

    internal NearbyDevice(MCPeerID mCPeerID)
    {
        _mCPeerID = mCPeerID;
        Id = mCPeerID
        DisplayName = mCPeerID.DisplayName;
    }
}