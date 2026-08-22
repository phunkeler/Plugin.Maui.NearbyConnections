namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Disposing the platform mid-handshake settles the pending accept — the handshake's TCS is
/// cancelled rather than left dangling, so a consumer awaiting <c>AcceptAsync</c> unblocks. Runs
/// the real <c>PlatformDispose</c> on-device, unlike the <c>net10.0</c> unit equivalent.
/// </summary>
public class DisposeTests : DeviceTest
{
    [Fact]
    public async Task DisposeMidHandshake_CancelsPendingAccept()
    {
        // Arrange — a real inbound handshake, paused between "request surfaced" and "peer accepted".
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await platform.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        _ = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        var (tcs, _) = platform._connectionTcs["endpoint-1"];

        // Act
        await platform.DisposeAsync();

        // Assert
        Assert.True(tcs.Task.IsCanceled);
    }
}
