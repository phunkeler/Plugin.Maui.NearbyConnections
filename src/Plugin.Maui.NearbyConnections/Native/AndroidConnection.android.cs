namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One established GMS link. Thin by design: the adapter still resolves the endpoint id per
/// operation, and this object is the per-link seam the bridge's connection table pairs with the
/// public connection (contract C7 — release disposes both in order).
/// </summary>
/// <param name="adapter">The Android adapter that owns the SDK client.</param>
/// <param name="deviceId">The device id this library minted for the link.</param>
sealed class AndroidConnection(AndroidAdapter adapter, string deviceId) : IPlatformConnection
{
    int _disposed;

    public Task SendBytesAsync(byte[] data, CancellationToken cancellationToken)
        => adapter.SendBytesAsync(deviceId, data, cancellationToken);

    public Task SendFileAsync(string uri, IProgress<NearbyTransferProgress>? progress, CancellationToken cancellationToken)
        => adapter.SendFileAsync(deviceId, uri, progress, cancellationToken);

    public Task<Stream> OpenStreamAsync(string name, CancellationToken cancellationToken)
        => adapter.OpenStreamAsync(deviceId, name, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await adapter.DisconnectEndpointAsync(deviceId).ConfigureAwait(false);
    }
}
