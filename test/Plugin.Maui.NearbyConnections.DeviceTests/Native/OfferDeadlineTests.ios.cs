namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The accept path's bound on iOS is the offer's remaining window — the deadline the initiator
/// declared in the invitation context. Multipeer Connectivity's native invitation timeout bounds
/// the <em>inviting</em> side only, so once this device calls the invitation handler nothing
/// platform-side ends the wait. The offer's deadline is what does.
/// </summary>
/// <remarks>
/// Real time, not a fake clock: these run against the real platform partial, so the declared
/// windows are deliberately short rather than injected.
/// </remarks>
public class OfferDeadlineTests : DeviceTest
{
    /// <summary>An invitation whose context declares a one-second offer window.</summary>
    static NSData ShortWindowContext()
        => NSData.FromArray(ControlMessage.EncodeConnectRequest(TimeSpan.FromSeconds(1), displayName: string.Empty));

    [Fact]
    public async Task AcceptedInvitation_WithNoTerminalCallback_TimesOutAtTheOfferDeadline()
    {
        // Arrange
        await using var platform = Create.PlatformBridge();
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var context = ShortWindowContext();

        platform.Ios().DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerID,
            context: context,
            invitationHandler: (_, _) => { });

        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act — accept, then never deliver OnPeerStateChanged.
        var pending = request.AcceptAsync(CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => pending);
    }

    [Fact]
    public async Task OfferDeadline_ClearsThePendingHandshakeEntry()
    {
        // Arrange
        await using var platform = Create.PlatformBridge();
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var context = ShortWindowContext();

        platform.Ios().DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerID,
            context: context,
            invitationHandler: (_, _) => { });

        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(
            () => request.AcceptAsync(CancellationToken.None));

        // Assert
        Assert.Empty(platform._connectionTcs);
    }

    [Fact]
    public async Task OfferDeadline_ResolvesTheInvitationHandlerNegatively()
    {
        // Arrange — MPC holds the invitation open until its handler is resolved. The timeout path
        // must release it, or the remote side is left waiting on a dead offer.
        await using var platform = Create.PlatformBridge();
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var context = ShortWindowContext();
        var accepted = new List<bool>();

        platform.Ios().DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerID,
            context: context,
            invitationHandler: (accept, _) => accepted.Add(accept));

        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(
            () => request.AcceptAsync(CancellationToken.None));

        // Assert — accepted once, then declined when the deadline elapsed.
        Assert.Equal([true, false], accepted);
    }
}
