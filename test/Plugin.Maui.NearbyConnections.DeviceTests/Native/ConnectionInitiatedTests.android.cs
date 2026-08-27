namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// First leg of an inbound connection on Android: <c>OnConnectionInitiatedAsync</c> must surface a
/// <see cref="NearbyConnectionRequest"/> on the advertise channel, with the offer's deadline
/// derived from the connect-request frame in the <c>ConnectionInfo</c>'s endpoint-info bytes — or
/// from the default window when the initiator sent a plain name (a legacy peer). Exercised against
/// the real platform partial with real SDK callback types — no live radio.
/// </summary>
public class ConnectionInitiatedTests : DeviceTest
{
    [Fact]
    public async Task IncomingConnection_WithAPlainName_YieldsRequestWithTheDefaultWindow()
    {
        // Arrange
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var before = DateTimeOffset.UtcNow;

        // Act — a legacy peer: the endpoint-info bytes are a raw UTF-8 name, not a frame.
        await platform.Android().OnConnectionInitiatedAsync("endpoint-1", Create.ConnectionInfo());

        // Assert
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal("Alice", request.RemoteDevice.DisplayName);
        Assert.InRange(
            request.Deadline,
            before + OfferWindow.Default,
            DateTimeOffset.UtcNow + OfferWindow.Default);
    }

    [Fact]
    public async Task IncomingConnection_WithAFrame_YieldsRequestWithTheDeclaredWindowAndName()
    {
        // Arrange — 100 ms (0x64 00 00 00): every frame byte stays string-safe, and the declared
        // window is unmistakably smaller than the default.
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var window = TimeSpan.FromMilliseconds(100);
        var before = DateTimeOffset.UtcNow;

        // Act
        await platform.Android().OnConnectionInitiatedAsync(
            "endpoint-1",
            Create.ConnectionInfoWithFrame(window, "Bob"));

        // Assert — the frame's name and window govern, not the raw endpoint-name decode. The
        // clamp itself is shared code, pinned by the unit suite — the device value here is only
        // that the frame reaches the adapter through the real SDK type.
        var request = await platform._advertiseChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal("Bob", request.RemoteDevice.DisplayName);
        Assert.InRange(request.Deadline, before + window, DateTimeOffset.UtcNow + window);
    }
}
