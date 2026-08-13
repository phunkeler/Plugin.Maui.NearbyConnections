namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// First leg of an inbound connection on iOS: <c>DidReceiveInvitationFromPeer</c> must surface a
/// <see cref="NearbyConnectionRequest"/> on the advertise channel. Exercised against the real
/// platform partial with real SDK callback types — no live radio.
/// </summary>
public class ConnectionInitiatedTests
{
    [Fact]
    public async Task IncomingInvitation_YieldsRequestOnAdvertiseChannel()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerId,
            context: null,
            invitationHandler: (_, _) => { });

        // Assert
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal("Alice", request.RemoteDevice.DisplayName);
    }
}
