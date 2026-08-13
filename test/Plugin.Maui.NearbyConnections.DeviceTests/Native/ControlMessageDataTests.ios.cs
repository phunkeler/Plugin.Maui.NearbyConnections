namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Control frames arriving through <c>OnDataReceived</c> are consumed by the plugin, never
/// surfaced to the consumer as payloads. (Named to avoid colliding with the unit suite's
/// <c>ControlMessageTests</c>, which covers Encode/TryDecode on <c>net10.0</c>.)
/// </summary>
public class ControlMessageDataTests
{
    [Fact]
    public async Task DisconnectControlFrame_IsNotSurfacedAsPayload()
    {
        // Arrange — live connection, then a control frame instead of app data.
        var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        // Act — with no MCSession live, HandleControlMessage's Disconnect is a no-op; the frame
        // must still be swallowed rather than routed.
        using var controlData = NSData.FromArray(ControlMessage.Encode(ControlMessageType.Disconnect));
        platform.OnDataReceived(controlData, peerId);

        // Assert — bounded negative read: nothing arrives on the receive stream.
        var received = await Receive.FirstOrNullAsync(connection, TimeSpan.FromMilliseconds(250));
        Assert.Null(received);
    }

    [Fact]
    public async Task UnknownControlType_IsSwallowedWithoutThrowing()
    {
        // Arrange
        var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        var frame = ControlMessage.Encode(ControlMessageType.Disconnect);
        frame[^1] = 0xFF; // valid signature, unknown control type

        // Act
        using var controlData = NSData.FromArray(frame);
        platform.OnDataReceived(controlData, peerId);

        // Assert
        var received = await Receive.FirstOrNullAsync(connection, TimeSpan.FromMilliseconds(250));
        Assert.Null(received);
    }
}
