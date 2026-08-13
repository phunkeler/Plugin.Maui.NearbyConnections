namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Inbound transfer-progress plumbing on Android: an <c>InProgress</c> update for a known inbound
/// payload reaches <see cref="NearbyConnection.InboundProgress"/>, and a file payload's
/// <c>Success</c> completes the copy-and-route pipeline end to end with real Java file handles.
/// </summary>
public class TransferUpdateTests
{
    [Fact]
    public async Task InProgressUpdate_ForInboundPayload_ReachesInboundProgress()
    {
        // Arrange — live connection with a pending inbound payload.
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (connection, id) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        var reported = new TaskCompletionSource<NearbyTransferProgress>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.InboundProgress = new Progress<NearbyTransferProgress>(p => reported.TrySetResult(p));

        var payload = Payload.FromBytes([1, 2, 3]);
        platform.OnPayloadReceived(id, payload);

        // Act
        await platform.OnPayloadTransferUpdate(
            id, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.InProgress, total: 3, transferred: 1));

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
        var receivedDir = Directory.CreateTempSubdirectory("devtest-received").FullName;
        await using var platform = Create.PlatformNearby(new NearbyOptions { ServiceId = "devtest", ReceivedFilesDirectory = receivedDir });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (connection, id) = await Create.ConnectedAsync(platform, "Alice", cts.Token);

        byte[] expected = [10, 20, 30];
        var sourcePath = Path.Combine(Path.GetTempPath(), $"devtest-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(sourcePath, expected, cts.Token);
        using var javaFile = new Java.IO.File(sourcePath);
        var payload = Payload.FromFile(javaFile);

        // Act — receipt then success, the order GMS delivers them; the platform owns the payload.
        platform.OnPayloadReceived(id, payload);
        await platform.OnPayloadTransferUpdate(
            id, Create.TransferUpdate(payload.Id, PayloadTransferUpdate.Status.Success, total: 3, transferred: 3));

        var received = await Receive.FirstAsync(connection, cts.Token);

        // Assert
        var filePayload = Assert.IsType<NearbyFilePayload>(received);
        Assert.Equal(expected, await File.ReadAllBytesAsync(filePayload.FileResult.FullPath, cts.Token));
        Assert.StartsWith(receivedDir, filePayload.FileResult.FullPath, StringComparison.Ordinal);
    }
}
