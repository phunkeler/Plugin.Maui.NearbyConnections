namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound bytes payloads on Android (<c>OnPayloadReceived</c> followed by
/// <c>OnPayloadTransferUpdate(Success)</c>, the order GMS delivers them) are routed to the right
/// connection's receive stream and consumed through the public
/// <see cref="NearbyConnection.ReceiveAsync"/> surface.
/// </summary>
public class PayloadRoutingTests
{
    [Fact]
    public async Task BytesPayload_RoutedToActiveConnectionReceiveStream()
    {
        // Arrange — a live connection, and a payload GMS hands over ownership of.
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, id) = await Create.ConnectedAsync(platform, "Alice", cts.Token);
        byte[] expected = [1, 2, 3];
        var payload = Payload.FromBytes(expected);

        // Act
        platform.OnPayloadReceived(id, payload);
        await platform.OnPayloadTransferUpdate(id, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success));

        // Assert
        var received = await Receive.FirstAsync(connection, cts.Token);
        var bytesPayload = Assert.IsType<NearbyBytesPayload>(received);
        Assert.Equal(expected, bytesPayload.Data);
    }
}
