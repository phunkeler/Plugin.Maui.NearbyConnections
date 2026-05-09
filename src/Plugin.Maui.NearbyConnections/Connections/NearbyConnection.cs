using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents an established peer-to-peer session with a remote device.
/// Obtained by calling <see cref="NearbyConnectionRequest.AcceptAsync"/> (advertiser side)
/// or the connect method on <see cref="INearbyConnections"/> (discoverer side).
/// Dispose via <see cref="DisposeAsync"/> to cleanly disconnect from the remote device.
/// </summary>
public sealed class NearbyConnection : IAsyncDisposable
{
    readonly Channel<NearbyPayload> _receiveChannel;
    readonly Func<byte[], CancellationToken, Task> _sendBytesFactory;
    readonly Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task> _sendFileFactory;
    readonly Func<ValueTask> _disposeFactory;
    readonly TaskCompletionSource _disconnectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int _disposeGuard;

    /// <summary>
    /// Gets the remote device this connection is established with.
    /// </summary>
    public NearbyDevice RemoteDevice { get; }

    /// <summary>
    /// A task that completes when this connection terminates, from either side, for any reason.
    /// Safe to await concurrently alongside <see cref="ReceiveAsync"/>. Does not consume the receive stream.
    /// </summary>
    public Task Disconnected => _disconnectedTcs.Task;

    /// <summary>
    /// Initializes a new <see cref="NearbyConnection"/> for use in test doubles of <see cref="INearbyConnections"/>.
    /// </summary>
    /// <param name="remoteDevice">The remote device this connection represents.</param>
    /// <param name="receiveChannel">The channel that delivers inbound payloads to <see cref="ReceiveAsync"/>.</param>
    /// <param name="sendBytesFactory">A delegate invoked when <see cref="SendAsync(byte[],CancellationToken)"/> is called.</param>
    /// <param name="sendFileFactory">A delegate invoked when <see cref="SendAsync(string,IProgress{NearbyTransferProgress}?,CancellationToken)"/> is called.</param>
    /// <param name="disposeFactory">A delegate invoked when <see cref="DisposeAsync"/> is called.</param>
    public NearbyConnection(
        NearbyDevice remoteDevice,
        Channel<NearbyPayload> receiveChannel,
        Func<byte[], CancellationToken, Task> sendBytesFactory,
        Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task> sendFileFactory,
        Func<ValueTask> disposeFactory)
    {
        RemoteDevice = remoteDevice;
        _receiveChannel = receiveChannel;
        _sendBytesFactory = sendBytesFactory;
        _sendFileFactory = sendFileFactory;
        _disposeFactory = disposeFactory;
    }

    /// <summary>
    /// Sends raw bytes to the remote device.
    /// </summary>
    /// <param name="data">
    /// The bytes to send. Limited to 32 KB on Android; use
    /// <see cref="SendAsync(string, IProgress{NearbyTransferProgress}?, CancellationToken)"/>
    /// for larger payloads.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the send operation.</param>
    /// <returns>A task that completes when the bytes have been handed off to the platform.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return _sendBytesFactory(data, cancellationToken);
    }

    /// <summary>
    /// Sends the file identified by <paramref name="fileUri"/> to the remote device.
    /// </summary>
    /// <param name="fileUri">A URI string identifying the file resource to send.</param>
    /// <param name="progress">
    /// An optional callback to receive outgoing transfer progress updates.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the transfer.</param>
    /// <returns>A task that completes when the transfer is fully enqueued or finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="fileUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public Task SendAsync(
        string fileUri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileUri);
        return _sendFileFactory(fileUri, progress, cancellationToken);
    }

    /// <summary>
    /// Sends the file represented by <paramref name="fileResult"/> to the remote device.
    /// This overload is symmetric with the <see cref="FilePayload"/> type yielded by <see cref="ReceiveAsync"/>.
    /// </summary>
    /// <param name="fileResult">The file to send.</param>
    /// <param name="progress">An optional callback to receive outgoing transfer progress updates.</param>
    /// <param name="cancellationToken">A token to cancel the transfer.</param>
    /// <returns>A task that completes when the transfer is fully enqueued or finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="fileResult"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public Task SendAsync(
        FileResult fileResult,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileResult);
        return _sendFileFactory(fileResult.FullPath, progress, cancellationToken);
    }

    /// <summary>
    /// Gets or sets an optional callback invoked with progress updates for inbound file transfers.
    /// </summary>
    /// <remarks>
    /// This callback is invoked on the platform's callback thread. Marshal to the UI thread yourself
    /// if you need to update UI from this callback.
    /// </remarks>
    public Action<NearbyTransferProgress>? InboundProgress { get; set; }

    /// <summary>
    /// Returns an async stream of payloads received from the remote device.
    /// The enumerable completes when the peer disconnects or <see cref="DisposeAsync"/> is called.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel enumeration.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyPayload"/> items.</returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    public IAsyncEnumerable<NearbyPayload> ReceiveAsync(CancellationToken cancellationToken = default)
        => _receiveChannel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Disconnects from the remote device and releases all resources used by this connection.
    /// After disposal the receive stream completes and no further sends are possible.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the disconnect is signaled.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0) return;
        await _disposeFactory();
        CompleteReceive();
    }

    /// <summary>
    /// Completes the receive channel, signaling that no more payloads will arrive.
    /// Called by the platform when the remote peer disconnects.
    /// </summary>
    internal void CompleteReceive()
    {
        _disconnectedTcs.TrySetResult();
        _receiveChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Writes an incoming payload to the receive channel.
    /// A <see langword="false"/> return (channel already completed) is silently dropped.
    /// </summary>
    internal void TryWritePayload(NearbyPayload payload)
        => _receiveChannel.Writer.TryWrite(payload);
}
