namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Disposing the platform mid-handshake settles the pending accept — the handshake's TCS is
/// cancelled rather than left dangling, so a consumer awaiting <c>AcceptAsync</c> unblocks. Runs
/// the real <c>PlatformDispose</c> on-device, unlike the <c>net10.0</c> unit equivalent.
/// </summary>
/// <remarks>
/// In the <see cref="StagingTests.Name"/> collection: <c>DisposeAsync</c> sweeps the shared static
/// staging directory, so this must not run in parallel with other classes that stage files there.
/// </remarks>
[Collection(StagingTests.Name)]
public class DisposeTests : DeviceTest
{
    [Fact]
    public async Task DisposeMidHandshake_CancelsPendingAccept()
    {
        // Arrange — a real inbound handshake, paused between "request surfaced" and "peer accepted".
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await platform.Android().OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        _ = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        var (tcs, _) = platform._connectionTcs[platform.PeerLookup.DeviceIdFor("endpoint-1")];

        // Act
        await platform.DisposeAsync();

        // Assert
        Assert.True(tcs.Task.IsCanceled);
    }
}
