namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One established MultipeerConnectivity link. Disposal sends the in-band disconnect frame —
/// MultipeerConnectivity has no per-peer disconnect — then releases the peer's bookkeeping and
/// retires the session when it holds nothing else.
/// </summary>
/// <param name="adapter">The iOS adapter that owns the session.</param>
/// <param name="deviceId">The device id this library minted for the link.</param>
sealed class IosConnection(IosAdapter adapter, string deviceId) : IPlatformConnection
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

        await adapter.DisconnectPeerAsync(deviceId).ConfigureAwait(false);
    }
}
