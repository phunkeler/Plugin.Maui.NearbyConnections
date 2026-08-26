namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

public class StreamPayloadTests : DeviceTest
{
    static Payload StreamPayloadFrom(byte[] contents)
    {
        var pipe = Android.OS.ParcelFileDescriptor.CreatePipe()!;
        using (var writer = new Android.Runtime.OutputStreamInvoker(
            new Android.OS.ParcelFileDescriptor.AutoCloseOutputStream(pipe[1])))
        {
            writer.Write(contents, 0, contents.Length);
        }

        return Payload.FromStream(pipe[0])!;
    }

    [Fact]
    public async Task NameFrameThenStream_DeliversANamedStreamPayload()
    {
        // Arrange
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);
        byte[] expected = [1, 2, 3, 4];
        var streamPayload = StreamPayloadFrom(expected);
        var frame = ControlMessage.EncodeStreamName(streamPayload.Id, "vitals");
        using var framePayload = Payload.FromBytes(frame)!;

        // Act — the name frame completes first, then the stream arrives.
        platform.Android().OnPayloadReceived(endpointId, framePayload);
        await platform.Android().OnPayloadTransferUpdate(
            endpointId, Create.TransferUpdate(framePayload.Id, PayloadTransferUpdate.Status.Success));
        platform.Android().OnPayloadReceived(endpointId, streamPayload);

        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert
        var stream = Assert.IsType<NearbyStreamPayload>(received);
        Assert.Equal("vitals", stream.Name);
        using var memory = new MemoryStream();
        await stream.Stream.CopyToAsync(memory, cts.Token);
        Assert.Equal(expected, memory.ToArray());
    }

    [Fact]
    public async Task StreamThenNameFrame_ParksAndDeliversOnce()
    {
        // The race's other half: the stream payload lands before its name frame finishes.

        // Arrange
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Bob", cts.Token);
        byte[] expected = [9, 8, 7];
        var streamPayload = StreamPayloadFrom(expected);
        var frame = ControlMessage.EncodeStreamName(streamPayload.Id, "telemetry");
        using var framePayload = Payload.FromBytes(frame)!;

        // Act — stream first, then the frame.
        platform.Android().OnPayloadReceived(endpointId, streamPayload);
        platform.Android().OnPayloadReceived(endpointId, framePayload);
        await platform.Android().OnPayloadTransferUpdate(
            endpointId, Create.TransferUpdate(framePayload.Id, PayloadTransferUpdate.Status.Success));

        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert
        var stream = Assert.IsType<NearbyStreamPayload>(received);
        Assert.Equal("telemetry", stream.Name);
        using var memory = new MemoryStream();
        await stream.Stream.CopyToAsync(memory, cts.Token);
        Assert.Equal(expected, memory.ToArray());
    }

}
