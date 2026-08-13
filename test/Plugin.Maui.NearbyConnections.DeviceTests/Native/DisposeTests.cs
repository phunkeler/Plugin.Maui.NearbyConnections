namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Disposing the platform mid-handshake settles the pending accept — the handshake's TCS is
/// cancelled rather than left dangling, so a consumer awaiting <c>AcceptAsync</c> unblocks. Runs
/// the real <c>PlatformDispose</c> on-device, unlike the <c>net10.0</c> unit equivalent.
/// </summary>
public class DisposeTests
{
    [Fact]
    public async Task DisposeMidHandshake_CancelsPendingAccept()
    {
        // Arrange — a real inbound handshake, paused between "request surfaced" and "peer accepted".
        var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

#if ANDROID
        const string id = "endpoint-1";
        await platform.OnConnectionInitiatedAsync(id, Create.ConnectionInfo());
        _ = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        var (tcs, _) = platform._connectionTcs[id];

        // Act
        await platform.DisposeAsync();

        // Assert
        Assert.True(tcs.Task.IsCanceled);
#elif IOS
        using var peerId = Create.PeerId("Alice");
        platform.DidReceiveInvitationFromPeer(
            advertiser: null!,
            peerID: peerId,
            context: null,
            invitationHandler: (_, _) => { });
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // AcceptAsync registers the TCS, constructs the real MCSession, and awaits the handshake
        // that will now never complete.
        var acceptTask = request.AcceptAsync(cts.Token);
        while (platform._connectionTcs.IsEmpty && !cts.IsCancellationRequested)
        {
            await Task.Delay(10, cts.Token);
        }

        // Act
        await platform.DisposeAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acceptTask);
#endif
    }
}
