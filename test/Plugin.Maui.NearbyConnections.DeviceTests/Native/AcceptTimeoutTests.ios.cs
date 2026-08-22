namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The accept path's own deadline on iOS. Multipeer Connectivity's native invitation timeout bounds
/// the <em>inviting</em> side only, so once this device calls the invitation handler nothing
/// platform-side ends the wait. <see cref="NearbyOptions.AcceptTimeout"/> is what does.
/// </summary>
/// <remarks>
/// Real time, not a fake clock: these run against the real platform partial, so the timeouts are
/// deliberately short rather than injected.
/// </remarks>
public class AcceptTimeoutTests : DeviceTest
{
    static NearbyOptions Options(TimeSpan accept) => new()
    {
        ServiceId = "devicetests",
        AcceptTimeout = accept,
    };

    [Fact]
    public async Task AcceptedInvitation_WithNoTerminalCallback_TimesOutInsteadOfHanging()
    {
        // Arrange
        await using var platform = Create.PlatformNearby(Options(TimeSpan.FromSeconds(1)));
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        platform.DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerId,
            context: null,
            invitationHandler: (_, _) => { });

        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act — accept, then never deliver OnPeerStateChanged.
        var pending = request.AcceptAsync(CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => pending);
    }

    [Fact]
    public async Task AcceptTimeout_ClearsThePendingHandshakeEntry()
    {
        // Arrange
        await using var platform = Create.PlatformNearby(Options(TimeSpan.FromSeconds(1)));
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        platform.DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerId,
            context: null,
            invitationHandler: (_, _) => { });

        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(
            () => request.AcceptAsync(CancellationToken.None));

        // Assert
        Assert.Empty(platform._connectionTcs);
    }

    [Fact]
    public async Task AcceptTimeout_ResolvesTheInvitationHandlerNegatively()
    {
        // Arrange — MPC holds the invitation open until its handler is resolved. The timeout path
        // must release it, or the remote side is left waiting on a dead offer.
        await using var platform = Create.PlatformNearby(Options(TimeSpan.FromSeconds(1)));
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var accepted = new List<bool>();

        platform.DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerId,
            context: null,
            invitationHandler: (accept, _) => accepted.Add(accept));

        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(
            () => request.AcceptAsync(CancellationToken.None));

        // Assert — accepted once, then declined when the deadline elapsed.
        Assert.Equal([true, false], accepted);
    }
}
