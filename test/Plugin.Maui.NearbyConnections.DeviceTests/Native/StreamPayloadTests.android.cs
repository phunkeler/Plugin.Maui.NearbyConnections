namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Story S8 on Android: a stream payload and its in-band name frame arrive as two GMS payloads,
/// in either order, and exactly one <c>NearbyStreamPayload</c> reaches the connection with the
/// name attached.
/// </summary>
/// <remarks>
/// <para>
/// Both orders are driven sequentially, which is this suite's contract — callbacks are invoked
/// directly, one at a time.
/// </para>
/// <para>
/// The genuinely <em>concurrent</em> case has no automated test, deliberately. Both halves enter
/// through different callbacks, so nothing serialises them, and the adapter must
/// test-for-partner-and-park under one lock or both halves can miss each other and park forever.
/// Driving that from two thread-pool tasks wedged the runner app — it stopped reporting rather
/// than failing a test — and the suite runs serially by design. The unit suite cannot cover it
/// either: it targets <c>net10.0</c>, where <c>AndroidAdapter</c> does not exist. The invariant is
/// held by the lock and its comment in <c>AndroidAdapter.android.cs</c>. Do not split that lock.
/// </para>
/// </remarks>
public class StreamPayloadTests : DeviceTest
{
    /// <summary>
    /// A GMS stream payload whose contents are already written and whose write end is closed, so a
    /// reader sees exactly <paramref name="contents"/> and then end-of-stream.
    /// </summary>
    /// <param name="contents">The bytes a reader of the payload will see.</param>
    static Payload StreamPayloadFrom(byte[] contents)
    {
        var pipe = Android.OS.ParcelFileDescriptor.CreatePipe()!;

        // AutoCloseOutputStream closes pipe[1] on dispose, which is what ends the reader's stream.
        using (var writer = new Android.OS.ParcelFileDescriptor.AutoCloseOutputStream(pipe[1]))
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
