namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One established platform link, produced by the adapter at establishment with its native handle
/// captured once — no per-send lookup by device id. The bridge's connection table maps a device id
/// to the pair (<see cref="NearbyConnection"/>, <see cref="IPlatformConnection"/>), public object
/// and platform object together, so release disposes both in order (contract C7).
/// </summary>
/// <remarks>
/// The session/connection split every surveyed transport converges on —
/// <c>NWListener</c>/<c>NWConnection</c>, Kestrel's listener/<c>ConnectionContext</c> — adopted in
/// <c>docs/ARCHITECTURE.md</c> section 4. Story S8's <c>OpenStreamAsync</c> lands here in stage
/// M6, for the same reason those models open streams from the connection object.
/// </remarks>
interface IPlatformConnection : IAsyncDisposable
{
    /// <summary>Sends raw bytes over this link. Throws the typed transfer failure.</summary>
    /// <param name="data">The bytes to send.</param>
    /// <param name="cancellationToken">A token to abandon the send.</param>
    Task SendBytesAsync(byte[] data, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a file over this link, reporting progress, bounded by
    /// <see cref="NearbyOptions.TransferInactivityTimeout"/>. Throws the typed transfer failure.
    /// </summary>
    /// <param name="uri">The file to send, as a <c>file://</c> URI (or <c>content://</c> on Android).</param>
    /// <param name="progress">Receives transfer progress, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">A token to abandon the transfer.</param>
    Task SendFileAsync(string uri, IProgress<NearbyTransferProgress>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a named one-way byte stream to the remote device (story S8). The name travels
    /// in-band on Android and on the native carrier on iOS; the remote side receives a
    /// <see cref="NearbyStreamPayload"/>.
    /// </summary>
    /// <param name="name">The stream's name, at most 1024 UTF-8 bytes on the wire.</param>
    /// <param name="cancellationToken">A token to abandon opening.</param>
    /// <returns>The writable stream. Disposing it ends the stream for the remote reader.</returns>
    Task<Stream> OpenStreamAsync(string name, CancellationToken cancellationToken);
}
