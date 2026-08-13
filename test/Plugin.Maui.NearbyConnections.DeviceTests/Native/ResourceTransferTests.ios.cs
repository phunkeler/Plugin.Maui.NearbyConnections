namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound resource (file) transfer callbacks: <c>OnResourceStarted</c> wires real KVO progress
/// through to <see cref="NearbyConnection.InboundProgress"/>, and <c>OnResourceFinished</c> copies
/// the received file into <see cref="NearbyOptions.ReceivedFilesDirectory"/> and routes a
/// <see cref="NearbyFilePayload"/>.
/// </summary>
public class ResourceTransferTests
{
    [Fact]
    public async Task ResourceFinished_CopiesFileAndRoutesFilePayload()
    {
        // Arrange — a live connection and a real source file where MPC would have staged it.
        var receivedDir = Directory.CreateTempSubdirectory("devtest-received").FullName;
        var platform = Create.PlatformNearby(new NearbyOptions { ServiceId = "devtest", ReceivedFilesDirectory = receivedDir });
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        byte[] expected = [10, 20, 30];
        var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(sourcePath, expected, cts.Token);

        // Act
        using var sourceUrl = NSUrl.FromFilename(sourcePath);
        platform.OnResourceFinished("photo.bin", peerId, sourceUrl, error: null);

        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert — payload routed, content copied to the received directory, staging file removed.
        var filePayload = Assert.IsType<NearbyFilePayload>(received);
        Assert.Equal(expected, await File.ReadAllBytesAsync(filePayload.FileResult.FullPath, cts.Token));
        Assert.StartsWith(receivedDir, filePayload.FileResult.FullPath, StringComparison.Ordinal);
        Assert.False(File.Exists(sourcePath));
    }

    [Fact]
    public async Task ResourceFinishedWithError_RoutesNothing()
    {
        // Arrange
        var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        // Act
        using var error = new NSError((NSString)"devtest", code: 42);
        platform.OnResourceFinished("photo.bin", peerId, localUrl: null, error);

        // Assert
        var received = await Receive.FirstOrNullAsync(connection, TimeSpan.FromMilliseconds(250));
        Assert.Null(received);
    }

    [Fact]
    public async Task ResourceStarted_RealKvoProgressReachesInboundProgress()
    {
        // Arrange — a live connection with an inbound-progress observer attached.
        var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerId, cts.Token);

        var reported = new TaskCompletionSource<NearbyTransferProgress>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.InboundProgress = new Progress<NearbyTransferProgress>(p => reported.TrySetResult(p));

        using var progress = NSProgress.FromTotalUnitCount(100);

        // Act — the real KVO registration fires when the native progress advances.
        platform.OnResourceStarted("photo.bin", peerId, progress);
        progress.CompletedUnitCount = 50;

        var update = await reported.Task.WaitAsync(cts.Token);

        // Assert
        Assert.Equal(NearbyTransferStatus.InProgress, update.Status);
        Assert.Equal(100, update.TotalBytes);
        Assert.Equal(50, update.BytesTransferred);

        // Cleanup the KVO registration through the path a dropped peer takes.
        platform.OnPeerStateChanged(peerId, MCSessionState.NotConnected);
    }
}
