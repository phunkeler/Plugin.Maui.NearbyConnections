namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound bytes payloads on iOS (<c>OnDataReceived</c>) are routed to the right connection's
/// receive stream and consumed through the public <see cref="NearbyConnection.ReceiveAsync"/>
/// surface.
/// </summary>
public class PayloadRoutingTests : DeviceTest
{
    [Fact]
    public async Task BytesPayload_RoutedToActiveConnectionReceiveStream()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerId, cts.Token);
        byte[] expected = [1, 2, 3];
        using var data = NSData.FromArray(expected);

        // Act
        platform.OnDataReceived(data, peerId);

        // Assert
        var received = await Receive.FirstAsync(connection, cts.Token);
        var bytesPayload = Assert.IsType<NearbyBytesPayload>(received);
        Assert.Equal(expected, bytesPayload.Data);
    }
}
