namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// First leg of an inbound connection: the platform's "someone wants to connect" callback
/// (<c>OnConnectionInitiatedAsync</c> on Android, <c>DidReceiveInvitationFromPeer</c> on iOS) must
/// surface a <see cref="NearbyConnectionRequest"/> on the advertise channel. Exercised against the
/// real platform partial with real SDK callback types — no live radio.
/// </summary>
public class ConnectionInitiatedTests
{
    [Fact]
    public async Task IncomingConnection_YieldsRequestOnAdvertiseChannel()
    {
        // Arrange
        var platform = Create.PlatformNearby();

#if ANDROID
        // Google marks this ctor deprecated but still ships it in play-services-nearby at the
        // pinned binding version; a future package bump that removes it fails loudly here.
        var connectionInfo = Create.ConnectionInfo();

        // Act
        await platform.OnConnectionInitiatedAsync("endpoint-1", connectionInfo);
#elif IOS
        using var peerId = Create.PeerId("Alice");

        // Act
        platform.DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerId,
            context: null,
            invitationHandler: (_, _) => { });
#endif

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Assert
        Assert.Equal("Alice", request.RemoteDevice.DisplayName);
    }
}
