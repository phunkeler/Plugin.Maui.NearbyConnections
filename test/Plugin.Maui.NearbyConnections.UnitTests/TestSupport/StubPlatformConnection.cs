namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// The per-link platform half behind a <see cref="Create.Connection"/>: inert by default, with a
/// delegate per operation so a test can observe or fault the platform side of one connection.
/// </summary>
sealed class StubPlatformConnection(
    Func<byte[], CancellationToken, Task>? sendBytes = null,
    Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task>? sendFile = null,
    Func<ValueTask>? dispose = null,
    Func<string, CancellationToken, Task<Stream>>? openStream = null) : IPlatformConnection
{
    public Task SendBytesAsync(byte[] data, CancellationToken cancellationToken)
        => (sendBytes ?? ((_, _) => Task.CompletedTask))(data, cancellationToken);

    public Task SendFileAsync(string uri, IProgress<NearbyTransferProgress>? progress, CancellationToken cancellationToken)
        => (sendFile ?? ((_, _, _) => Task.CompletedTask))(uri, progress, cancellationToken);

    public Task<Stream> OpenStreamAsync(string name, CancellationToken cancellationToken)
        => (openStream ?? ((_, _) => Task.FromResult<Stream>(new MemoryStream())))(name, cancellationToken);

    public ValueTask DisposeAsync()
        => (dispose ?? (() => ValueTask.CompletedTask))();
}
