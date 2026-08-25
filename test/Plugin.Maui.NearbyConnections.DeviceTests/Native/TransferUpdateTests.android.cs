namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound transfer-progress plumbing on Android: an <c>InProgress</c> update for a known inbound
/// payload reaches <see cref="NearbyConnection.InboundProgress"/>, and a file payload's
/// <c>Success</c> completes the copy-and-route pipeline end to end with real Java file handles.
/// </summary>
[Collection(StagingTests.Name)]
public class TransferUpdateTests : DeviceTest
{
    [Fact]
    public async Task InProgressUpdate_ForInboundPayload_ReachesInboundProgress()
    {
        // Arrange — live connection with a pending inbound payload.
        await using var platform = Create.PlatformBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        var reported = new TaskCompletionSource<NearbyTransferProgress>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.InboundProgress = new Progress<NearbyTransferProgress>(p => reported.TrySetResult(p));

        var payload = Payload.FromBytes([1, 2, 3]);
        platform.Android().OnPayloadReceived(endpointId, payload);

        // Act
        await platform.Android().OnPayloadTransferUpdate(
            endpointId, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.InProgress, total: 3, transferred: 1));

        var update = await reported.Task.WaitAsync(cts.Token);

        // Assert
        Assert.Equal(NearbyTransferStatus.InProgress, update.Status);
        Assert.Equal(3, update.TotalBytes);
        Assert.Equal(1, update.BytesTransferred);
    }

    [Fact]
    public async Task FilePayloadSuccess_CopiesFileAndRoutesFilePayload()
    {
        // Arrange — live connection and a real file behind a real Java file handle.
        await using var platform = Create.PlatformBridge(new NearbyOptions { ServiceId = "devtest" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        byte[] expected = [10, 20, 30];
        var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(sourcePath, expected, cts.Token);
        using var javaFile = new Java.IO.File(sourcePath);
        var payload = Payload.FromFile(javaFile);

        // Act — receipt then success, the order GMS delivers them; the platform owns the payload.
        platform.Android().OnPayloadReceived(endpointId, payload);
        await platform.Android().OnPayloadTransferUpdate(
            endpointId, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success, total: 3, transferred: 3));

        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert
        var filePayload = Assert.IsType<NearbyFilePayload>(received);
        Assert.Equal(expected, await File.ReadAllBytesAsync(filePayload.FileResult.FullPath, cts.Token));
        Assert.StartsWith(platform.Android().StagingDirectory, filePayload.FileResult.FullPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoFilesWithTheSameName_BothSurvive()
    {
        // Arrange — the collision the reservation exists for: one peer sends photo.bin twice.
        await using var platform = Create.PlatformBridge(new NearbyOptions { ServiceId = "devtest" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        byte[] first = [1, 1, 1];
        byte[] second = [2, 2, 2];

        // Act — both sends before the single receive enumeration: ReceiveAsync is single-consumer.
        foreach (var content in new[] { first, second })
        {
            var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
            await File.WriteAllBytesAsync(sourcePath, content, cts.Token);
            using var javaFile = new Java.IO.File(sourcePath);
            var payload = Payload.FromFile(javaFile);

            platform.Android().OnPayloadReceived(endpointId, payload);
            await platform.Android().OnPayloadTransferUpdate(
                endpointId, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success, total: 3, transferred: 3));
        }

        var received = await Receive.TakeAsync(connection, 2, cts.Token);
        var paths = received.Select(p => Assert.IsType<NearbyFilePayload>(p).FileResult.FullPath).ToList();

        // Assert — distinct destinations, neither clobbered.
        Assert.NotEqual(paths[0], paths[1]);
        Assert.Equal(first, await File.ReadAllBytesAsync(paths[0], cts.Token));
        Assert.Equal(second, await File.ReadAllBytesAsync(paths[1], cts.Token));
    }

    [Fact]
    public async Task Dispose_RemovesAStagedFileNobodyMoved()
    {
        // Arrange — a delivered file the consumer never moved out of staging.
        var platform = Create.PlatformBridge(new NearbyOptions { ServiceId = "devtest" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (connection, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(sourcePath, [7, 7, 7], cts.Token);
        using var javaFile = new Java.IO.File(sourcePath);
        var payload = Payload.FromFile(javaFile);

        platform.Android().OnPayloadReceived(endpointId, payload);
        await platform.Android().OnPayloadTransferUpdate(
            endpointId, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success, total: 3, transferred: 3));

        var received = await Receive.FirstAsync(connection, cts.Token);
        var stagedPath = Assert.IsType<NearbyFilePayload>(received).FileResult.FullPath;

        // Act
        await platform.DisposeAsync();

        // Assert
        Assert.False(File.Exists(stagedPath));
    }

    [Fact]
    public async Task DisposeDuringACopy_LeavesNothingBehindInStaging()
    {
        // Arrange — a copy left in flight, the way GMS leaves one: the async void callback returns
        // at the copy's first await. A 4 MB payload keeps it there while disposal runs.
        //
        // This does not fail without the disposal drain, and it is kept as a guard rather than as a
        // regression test for it. Nothing observable distinguishes the two orderings here: a
        // cancelled copy deletes its own partial file, and PlatformDispose clears its state either
        // way, so the directory ends up empty whether or not the sweep waited. What it does pin is
        // that disposing mid-copy stays clean — no orphan, no throw, no hang.
        var platform = Create.PlatformBridge(new NearbyOptions { ServiceId = "devtest" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (_, endpointId, _) = await Create.ConnectedAsync(platform, "Alice", cts.Token);
        var payload = Create.FilePayload(new byte[4 * 1024 * 1024], $"drain-{Guid.NewGuid():N}.bin");

        platform.Android().OnPayloadReceived(endpointId, payload);
        var copy = platform.Android().OnPayloadTransferUpdate(
            endpointId, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success));

        // Act
        await platform.DisposeAsync();
        await copy;

        // Assert
        Assert.Empty(Directory.Exists(platform.Android().StagingDirectory)
            ? Directory.GetFiles(platform.Android().StagingDirectory)
            : []);
    }
}
