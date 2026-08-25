namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound resource (file) transfer callbacks: <c>OnResourceStarted</c> wires real KVO progress
/// through to <see cref="NearbyConnection.InboundProgress"/>, and <c>OnResourceFinished</c> copies
/// the received file into the library's staging directory and routes a
/// <see cref="NearbyFilePayload"/>.
/// </summary>
[Collection(StagingTests.Name)]
public class ResourceTransferTests : DeviceTest
{
    [Fact]
    public async Task ResourceFinished_CopiesFileAndRoutesFilePayload()
    {
        // Arrange — a live connection and a real source file where MPC would have staged it.
        await using var platform = Create.PlatformNearby(new NearbyOptions { ServiceId = "devtest" });
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerID, cts.Token);

        byte[] expected = [10, 20, 30];
        var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(sourcePath, expected, cts.Token);

        // Act
        using var sourceUrl = NSUrl.FromFilename(sourcePath);
        platform.OnResourceFinished("photo.bin", peerID, sourceUrl, error: null);

        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert — payload routed, content moved into staging, MPC's own file consumed.
        var filePayload = Assert.IsType<NearbyFilePayload>(received);
        Assert.Equal(expected, await File.ReadAllBytesAsync(filePayload.FileResult.FullPath, cts.Token));
        Assert.StartsWith(PlatformNearby.StagingDirectory, filePayload.FileResult.FullPath, StringComparison.Ordinal);
        Assert.False(File.Exists(sourcePath));
    }

    [Fact]
    public async Task ResourceFinishedWithError_RoutesNothing()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerID, cts.Token);

        // Act
        using var error = new NSError((NSString)"devtest", code: 42);
        platform.OnResourceFinished("photo.bin", peerID, localUrl: null, error);

        // Assert
        await Receive.AssertNothingReceivedAsync(connection);
    }

    [Fact]
    public async Task ResourceStarted_RealKvoProgressReachesInboundProgress()
    {
        // Arrange — a live connection with an inbound-progress observer attached.
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerID, cts.Token);

        var reported = new TaskCompletionSource<NearbyTransferProgress>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.InboundProgress = new Progress<NearbyTransferProgress>(p => reported.TrySetResult(p));

        using var progress = NSProgress.FromTotalUnitCount(100);

        // Act — the real KVO registration fires when the native progress advances.
        platform.OnResourceStarted("photo.bin", peerID, progress);
        progress.CompletedUnitCount = 50;

        // Assert
        var update = await reported.Task.WaitAsync(cts.Token);
        Assert.Equal(NearbyTransferStatus.InProgress, update.Status);
        Assert.Equal(100, update.TotalBytes);
        Assert.Equal(50, update.BytesTransferred);
    }

    [Fact]
    public async Task TwoResourcesWithTheSameName_BothSurvive()
    {
        // Arrange — the collision the reservation exists for: one peer sends photo.bin twice.
        await using var platform = Create.PlatformNearby(new NearbyOptions { ServiceId = "devtest" });
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerID, cts.Token);

        byte[] first = [1, 1, 1];
        byte[] second = [2, 2, 2];
        var paths = new List<string>();

        // Act — both resources arrive under the same name, then both are read through the one
        // enumerator the receive stream allows.
        foreach (var content in new[] { first, second })
        {
            var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
            await File.WriteAllBytesAsync(sourcePath, content, cts.Token);

            using var sourceUrl = NSUrl.FromFilename(sourcePath);
            platform.OnResourceFinished("photo.bin", peerID, sourceUrl, error: null);
        }

        foreach (var received in await Receive.TakeAsync(connection, 2, cts.Token))
        {
            paths.Add(Assert.IsType<NearbyFilePayload>(received).FileResult.FullPath);
        }

        // Assert — distinct destinations, neither clobbered.
        Assert.NotEqual(paths[0], paths[1]);
        Assert.Equal(first, await File.ReadAllBytesAsync(paths[0], cts.Token));
        Assert.Equal(second, await File.ReadAllBytesAsync(paths[1], cts.Token));
    }

    [Fact]
    public async Task Dispose_RemovesAStagedFileNobodyMoved()
    {
        // Arrange — a delivered file the consumer never moved out of staging.
        var platform = Create.PlatformNearby(new NearbyOptions { ServiceId = "devtest" });
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, _) = await Create.ConnectedAsync(platform, peerID, cts.Token);

        var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(sourcePath, [7, 7, 7], cts.Token);

        using var sourceUrl = NSUrl.FromFilename(sourcePath);
        platform.OnResourceFinished("photo.bin", peerID, sourceUrl, error: null);

        var received = await Receive.FirstAsync(connection, cts.Token);
        var stagedPath = Assert.IsType<NearbyFilePayload>(received).FileResult.FullPath;

        // Act
        await platform.DisposeAsync();

        // Assert
        Assert.False(File.Exists(stagedPath));
    }
}
