namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Every iOS platform callback tolerates an unknown peer without throwing — the repo's "every catch
/// on a callback path logs" rule means a stray late callback must never take down the process. Each
/// Act would fail the test on throw; the assert pins the absence of side effects.
/// </summary>
public class UnknownPeerTests : DeviceTest
{
    [Fact]
    public async Task PeerStateChanged_ForUnknownPeer_LeavesNoState()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("never-seen");
        var id = platform.PeerLookup.DeviceIdFor(peerID);

        // Act
        platform.OnPeerStateChanged(peerID, MCSessionState.NotConnected);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform._advertiseChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task LostPeer_ForUnknownPeer_PublishesNoEvent()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("never-seen");
        var id = platform.PeerLookup.DeviceIdFor(peerID);

        // Act
        platform.LostPeer(browser: null!, peerID: peerID);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DataReceived_ForUnknownPeer_RoutesNothing()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("never-seen");
        var id = platform.PeerLookup.DeviceIdFor(peerID);
        using var data = NSData.FromArray([1, 2, 3]);

        // Act
        platform.OnDataReceived(data, peerID);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }
}
