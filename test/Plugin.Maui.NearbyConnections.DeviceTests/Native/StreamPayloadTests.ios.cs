namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Story S8 on iOS: the delegate's stream callback delivers a <c>NearbyStreamPayload</c> with the
/// natively carried name — no in-band frame on this platform.
/// </summary>
public class StreamPayloadTests : DeviceTest
{
    [Fact]
    public async Task ReceivedStream_DeliversANamedStreamPayload()
    {
        // Arrange — a connected peer, then a stream from it.
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var peerID = Create.PeerId("Alice");
        var (tcs, _) = Create.PendingHandshake(platform, peerID);
        platform.Ios().OnPeerStateChanged(peerID, MCSessionState.Connected);
        var connection = await tcs.Task.WaitAsync(cts.Token);
        byte[] expected = [1, 2, 3, 4];
        using var data = NSData.FromArray(expected);

        // Act
        platform.Ios().OnStreamReceived("vitals", peerID, NSInputStream.FromData(data));
        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert
        var stream = Assert.IsType<NearbyStreamPayload>(received);
        Assert.Equal("vitals", stream.Name);
        using var memory = new MemoryStream();
        await stream.Stream.CopyToAsync(memory, cts.Token);
        Assert.Equal(expected, memory.ToArray());
    }
}
