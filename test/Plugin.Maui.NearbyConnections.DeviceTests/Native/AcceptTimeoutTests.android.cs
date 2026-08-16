namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The accept path's own deadline on Android. <c>AcceptAsync</c> awaits a
/// <see cref="TaskCompletionSource{TResult}"/> that only a terminal GMS callback resolves, and a
/// device that leaves range mid-handshake produces no such callback. Without
/// <see cref="NearbyOptions.AcceptTimeout"/> the await never returns.
/// </summary>
/// <remarks>
/// Real time, not a fake clock: these run against the real platform partial, so the timeouts are
/// deliberately short rather than injected.
/// </remarks>
public class AcceptTimeoutTests
{
    static NearbyOptions Options(TimeSpan accept) => new()
    {
        ServiceId = "devicetests",
        AcceptTimeout = accept,
    };

    [Fact]
    public async Task AcceptedRequest_WithNoTerminalCallback_TimesOutInsteadOfHanging()
    {
        // Arrange
        var accept = TimeSpan.FromSeconds(1);
        await using var platform = Create.PlatformNearby(Options(accept));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await platform.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act — accept, then never deliver OnConnectionResult.
        var pending = request.AcceptAsync(CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => pending);
    }

    [Fact]
    public async Task AcceptTimeout_ClearsThePendingHandshakeEntry()
    {
        // Arrange
        var accept = TimeSpan.FromSeconds(1);
        await using var platform = Create.PlatformNearby(Options(accept));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await platform.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(
            () => request.AcceptAsync(CancellationToken.None));

        // Assert — a stranded entry would leak, and would make a later attempt to the same endpoint
        // resolve the wrong handshake.
        Assert.Empty(platform._connectionTcs);
    }

    [Fact]
    public async Task AcceptedRequest_WhenResultArrivesFirst_ReturnsTheConnection()
    {
        // Arrange — the deadline must not fire on a handshake that completes normally.
        await using var platform = Create.PlatformNearby(Options(TimeSpan.FromSeconds(30)));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await platform.OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);

        // Act
        var pending = request.AcceptAsync(cts.Token);
        platform.OnConnectionResult("endpoint-1", Create.Resolution());

        // Assert
        var connection = await pending;
        Assert.Equal("endpoint-1", connection.RemoteDevice.Id);
    }
}
