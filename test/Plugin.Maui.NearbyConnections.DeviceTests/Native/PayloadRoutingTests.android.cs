namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound bytes payloads on Android (<c>OnPayloadReceived</c> followed by
/// <c>OnPayloadTransferUpdate(Success)</c>, the order GMS delivers them) are routed to the right
/// connection's receive stream and consumed through the public
/// <see cref="NearbyConnection.ReceiveAsync"/> surface.
/// </summary>
public class PayloadRoutingTests : DeviceTest
{
    [Fact]
    public async Task BytesPayload_RoutedToActiveConnectionReceiveStream()
    {
        // Arrange — a live connection, and a payload GMS hands over ownership of.
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);
        byte[] expected = [1, 2, 3];
        var payload = Payload.FromBytes(expected);

        // Act
        platform.Android().OnPayloadReceived(endpointId, payload);
        await platform.Android().OnPayloadTransferUpdate(endpointId, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success));

        // Assert
        var received = await Receive.FirstAsync(connection, cts.Token);
        var bytesPayload = Assert.IsType<NearbyBytesPayload>(received);
        Assert.Equal(expected, bytesPayload.Data);
    }

    [Fact]
    public async Task FileCompletedBeforeBytes_ArrivesFirst()
    {
        // Arrange — a file whose copy suspends, then a bytes payload completing during that copy.
        // Not awaiting the file update is the point: it reproduces the async void callback
        // returning to GMS at the copy's first await, which is when GMS delivers the next update.
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);
        var file = Create.FilePayload(new byte[512 * 1024], $"ordering-{Guid.NewGuid():N}.bin");
        var bytes = Payload.FromBytes([1, 2, 3]);

        // Act
        platform.Android().OnPayloadReceived(endpointId, file);
        platform.Android().OnPayloadReceived(endpointId, bytes);
        var fileUpdate = platform.Android().OnPayloadTransferUpdate(endpointId, Create.TransferUpdate(file.Id, PayloadTransferUpdate.Status.Success));
        await platform.Android().OnPayloadTransferUpdate(endpointId, Create.TransferUpdate(bytes.Id, PayloadTransferUpdate.Status.Success));
        await fileUpdate;

        // Assert
        var received = await Receive.TakeAsync(connection, 2, cts.Token);
        Assert.IsType<NearbyFilePayload>(received[0]);
        Assert.IsType<NearbyBytesPayload>(received[1]);
    }
}
