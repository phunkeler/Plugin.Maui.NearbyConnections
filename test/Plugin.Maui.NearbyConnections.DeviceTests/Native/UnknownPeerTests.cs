namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Every platform callback tolerates an unknown peer/endpoint id without throwing — the repo's
/// "every catch on a callback path logs" rule means a stray late callback must never take down the
/// process. Each Act would crash the test on throw; the assert pins the absence of side effects.
/// </summary>
public class UnknownPeerTests
{
    [Fact]
    public async Task CallbacksForUnknownPeer_DoNotThrowAndLeaveNoState()
    {
        // Arrange
        var platform = Create.PlatformNearby();

#if ANDROID
        const string id = "never-seen";

        // Act
        platform.OnDisconnected(id);
        platform.OnEndpointLost(id);
        platform.OnConnectionResult(id, Create.Resolution());
        await platform.OnPayloadTransferUpdate(id, Create.TransferUpdate(payloadId: 42, PayloadTransferUpdate.Status.Success));
#elif IOS
        using var peerId = Create.PeerId("never-seen");
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);

        // Act
        platform.OnPeerStateChanged(peerId, MCSessionState.NotConnected);
        platform.LostPeer(browser: null!, peerID: peerId);
        using var data = NSData.FromArray([1, 2, 3]);
        platform.OnDataReceived(data, peerId);
        await Task.CompletedTask;
#endif

        // Assert
        Assert.False(platform._activeConnections.ContainsKey(id));
        Assert.False(platform._advertiseChannel.Reader.TryRead(out _));
        Assert.False(platform._discoverChannel.Reader.TryRead(out _));
    }
}
