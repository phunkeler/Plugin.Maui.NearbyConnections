namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound bytes payloads (<c>OnPayloadReceived</c> + <c>OnPayloadTransferUpdate(Success)</c> on
/// Android, <c>OnDataReceived</c> on iOS) are routed to the right connection's receive stream and
/// consumed through the public <see cref="NearbyConnection.ReceiveAsync"/> surface.
/// </summary>
public class PayloadRoutingTests
{
    [Fact]
    public async Task BytesPayload_RoutedToActiveConnectionReceiveStream()
    {
        // Arrange — establish a live connection through the real callback path.
        var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        byte[] expected = [1, 2, 3];

#if ANDROID
        var (connection, id) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        // Act — receipt then successful-transfer completion, the order GMS delivers them.
        // Ownership of the Payload transfers to the platform, which disposes it after processing.
        var payload = Payload.FromBytes(expected);
        platform.OnPayloadReceived(id, payload);
        await platform.OnPayloadTransferUpdate(id, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success));
#elif IOS
        using var peerId = Create.PeerId("Alice");
        var (connection, _) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        // Act
        using var data = NSData.FromArray(expected);
        platform.OnDataReceived(data, peerId);
#endif

        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert
        var bytesPayload = Assert.IsType<NearbyBytesPayload>(received);
        Assert.Equal(expected, bytesPayload.Data);
    }
}
