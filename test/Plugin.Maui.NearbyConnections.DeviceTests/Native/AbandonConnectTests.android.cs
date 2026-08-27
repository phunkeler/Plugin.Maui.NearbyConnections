namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// A handshake that exits through the catch-all — a cancelled caller, not the deadline — must run
/// the same abandon the deadline exit runs. Without it, a cancelled <c>ConnectAsync</c> left GMS
/// holding a half-open handshake, and the platform kept the peer's handles until process death.
/// On Android the abandon is observable in-process: it releases the connection and removes the
/// peer from <c>PeerLookup</c>.
/// </summary>
public class AbandonConnectTests : DeviceTest
{
    [Fact]
    public async Task CancelledHandshake_AbandonsThePlatformConnection()
    {
        // Arrange — a pending initiator handshake the platform tracks in PeerLookup.
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource();
        var (tcs, deviceId) = Create.PendingHandshake(platform);
        Assert.True(platform.PeerLookup.TryGetDevice(deviceId, out var device));
        Assert.True(platform.PeerLookup.TryGetEndpointId(deviceId, out _));

        // Act — cancel the caller's token while the platform never calls back.
        var pending = platform.AwaitHandshakeAsync(
            device!,
            tcs,
            ConnectionRole.Initiator,
            TimeSpan.FromSeconds(30),
            beforeAwait: static _ => Task.CompletedTask,
            cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        // Assert — the platform no longer tracks the peer: the catch-all exit abandoned the
        // half-open connection instead of only clearing the handshake entry.
        Assert.False(platform.PeerLookup.TryGetEndpointId(deviceId, out _));
        Assert.Empty(platform._connectionTcs);
    }
}
