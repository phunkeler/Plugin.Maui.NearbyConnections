namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Every iOS platform callback tolerates an unknown peer without throwing — the repo's "every catch
/// on a callback path logs" rule means a stray late callback must never take down the process. Each
/// Act would fail the test on throw; the assert pins the absence of side effects.
/// </summary>
public class UnknownPeerTests
{
    [Fact]
    public async Task PeerStateChanged_ForUnknownPeer_LeavesNoState()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("never-seen");
        var id = platform.Peers.PeerKey(peerId);

        // Act
        platform.OnPeerStateChanged(peerId, MCSessionState.NotConnected);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform._advertiseChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task LostPeer_ForUnknownPeer_PublishesNoEvent()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("never-seen");
        var id = platform.Peers.PeerKey(peerId);

        // Act
        platform.LostPeer(browser: null!, peerID: peerId);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DataReceived_ForUnknownPeer_RoutesNothing()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("never-seen");
        var id = platform.Peers.PeerKey(peerId);
        using var data = NSData.FromArray([1, 2, 3]);

        // Act
        platform.OnDataReceived(data, peerId);

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }
}
