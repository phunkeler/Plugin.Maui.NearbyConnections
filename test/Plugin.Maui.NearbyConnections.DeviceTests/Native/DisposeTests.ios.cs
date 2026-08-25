namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Disposing the platform mid-handshake settles the pending accept — the handshake's TCS is
/// cancelled rather than left dangling, so a consumer awaiting <c>AcceptAsync</c> unblocks. Runs
/// the real <c>PlatformDispose</c> on-device, unlike the <c>net10.0</c> unit equivalent.
/// </summary>
[Collection(StagingTests.Name)]
public class DisposeTests : DeviceTest
{
    [Fact]
    public async Task DisposeMidHandshake_CancelsPendingAccept()
    {
        // Arrange — a real inbound invitation accepted, so AcceptAsync is awaiting a handshake that
        // will never complete: it registers the TCS and constructs the real MCSession.
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        platform.DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerID,
            context: null,
            invitationHandler: (_, _) => { });
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        var acceptTask = request.AcceptAsync(cts.Token);
        await Create.WaitForPendingHandshakeAsync(platform, cts.Token);

        // Act
        await platform.DisposeAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acceptTask);
    }
}
