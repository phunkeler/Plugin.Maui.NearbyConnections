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
    readonly Func<byte[], CancellationToken, ValueTask> _sendBytesFactory;
    readonly Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task> _sendFileFactory;
    readonly Func<ValueTask> _disposeFactory;
    readonly TaskCompletionSource _disconnectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly CancellationTokenSource _disconnectedCts = new();

    int _disposeGuard;
    int _receiveGuard;

    /// <summary>
    /// Gets the remote device this connection is established with.
    /// </summary>
    public NearbyDevice RemoteDevice { get; }

    /// <summary>
    /// Gets the role this local device plays in this connection.
    /// <see cref="ConnectionRole.Initiator"/> when this device called ConnectAsync;
    /// <see cref="ConnectionRole.Acceptor"/> when this device called AcceptAsync.
    /// </summary>
    public ConnectionRole Role { get; internal set; }

    /// <summary>
    /// A task that completes when this connection terminates, from either side, for any reason.
    /// Safe to await concurrently alongside <see cref="ReceiveAsync"/>. Does not consume the receive stream.
    /// </summary>
    public Task Disconnected => _disconnectedTcs.Task;

    /// <summary>
    /// A token that is canceled when this connection terminates, from either side, for any reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The token form of <see cref="Disconnected"/>, for cancelling your own per-connection work
    /// when the peer goes away — a retry loop, a periodic ping, an upload you started on its behalf.
    /// </para>
    /// <para>
    /// <strong>Do not pass this to <see cref="ReceiveAsync"/>.</strong> It is unnecessary: the
    /// receive stream already ends by itself on disconnect. It is also harmful — cancellation is
    /// observed on every iteration, so a canceled token throws
    /// <see cref="OperationCanceledException"/> and discards payloads still buffered from just
    /// before the disconnect, which is precisely the data most worth keeping. Enumerate with no
    /// token (or one of your own) and let completion end the loop:
    /// <code>
    /// await foreach (var payload in connection.ReceiveAsync())
    /// {
    ///     await HandleAsync(payload);   // loop exits on its own when the peer disconnects
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Remains valid after <see cref="DisposeAsync"/> — reading it never throws
    /// <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    public CancellationToken DisconnectedToken => _disconnectedCts.Token;

    /// <summary>
    /// Initializes a new <see cref="NearbyConnection"/> for use in test doubles of <see cref="INearbyConnections"/>.
    /// </summary>
    /// <param name="remoteDevice">The remote device this connection represents.</param>
    /// <param name="receiveChannel">The channel that delivers inbound payloads to <see cref="ReceiveAsync"/>.</param>
    /// <param name="sendBytesFactory">A delegate invoked when <see cref="SendAsync(byte[],CancellationToken)"/> is called.</param>
    /// <param name="sendFileFactory">A delegate invoked when <see cref="SendAsync(string,IProgress{NearbyTransferProgress}?,CancellationToken)"/>, <see cref="SendAsync(FileResult,IProgress{NearbyTransferProgress}?,CancellationToken)"/>, or <see cref="SendAsync(NearbyPayload,IProgress{NearbyTransferProgress}?,CancellationToken)"/> is called with a file payload.</param>
    /// <param name="disposeFactory">A delegate invoked when <see cref="DisposeAsync"/> is called.</param>
    public NearbyConnection(
        NearbyDevice remoteDevice,
        Channel<NearbyPayload> receiveChannel,
        Func<byte[], CancellationToken, ValueTask> sendBytesFactory,
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
    /// <returns>A <see cref="ValueTask"/> that completes when the bytes have been handed off to the platform.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public ValueTask SendAsync(byte[] data, CancellationToken cancellationToken = default)
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
    /// <exception cref="NearbyTransferTimeoutException">Thrown when no transfer progress is observed for <see cref="NearbyConnectionsOptions.TransferInactivityTimeout"/> (default 10 seconds).</exception>
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
    /// <exception cref="NearbyTransferTimeoutException">Thrown when no transfer progress is observed for <see cref="NearbyConnectionsOptions.TransferInactivityTimeout"/> (default 10 seconds).</exception>
    public Task SendAsync(
        FileResult fileResult,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileResult);
        return _sendFileFactory(fileResult.FullPath, progress, cancellationToken);
    }

    /// <summary>
    /// Sends a <see cref="NearbyPayload"/> to the remote device.
    /// Dispatches to the appropriate platform send path based on the concrete payload type.
    /// This overload is symmetric with the values yielded by <see cref="ReceiveAsync"/>.
    /// </summary>
    /// <param name="payload">The payload to send. Must be a <see cref="BytesPayload"/> or <see cref="FilePayload"/>.</param>
    /// <param name="progress">
    /// An optional callback to receive outgoing transfer progress updates.
    /// Only used when <paramref name="payload"/> is a <see cref="FilePayload"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the send operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the payload has been handed off to the platform.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="payload"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="payload"/> is an unrecognised subtype.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="NearbyTransferTimeoutException">Thrown when <paramref name="payload"/> is a <see cref="FilePayload"/> and no transfer progress is observed for <see cref="NearbyConnectionsOptions.TransferInactivityTimeout"/> (default 10 seconds).</exception>
    public ValueTask SendAsync(
        NearbyPayload payload,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload switch
        {
            BytesPayload bytes => _sendBytesFactory(bytes.Data, cancellationToken),
            FilePayload file => new ValueTask(_sendFileFactory(file.FileResult.FullPath, progress, cancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(payload),
                $"Unsupported payload type '{payload.GetType().Name}'. Only {nameof(BytesPayload)} and {nameof(FilePayload)} are supported.")
        };
    }

    /// <summary>
    /// Gets or sets an optional progress handler invoked with progress updates for inbound file transfers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Asymmetry with outbound progress (by design).</strong>
    /// Outbound progress is supplied per-call as an <c>IProgress&lt;NearbyTransferProgress&gt;?</c>
    /// parameter on each <c>SendAsync</c> overload, because the caller initiates the transfer and
    /// knows up-front which handler to use.
    /// Inbound progress cannot follow the same pattern: file transfers arrive asynchronously from
    /// the platform on a background thread, before any consumer <c>await</c> has a chance to supply
    /// a handler. Exposing it as a settable property lets callers attach a handler immediately after
    /// accepting the connection and before any payload arrives.
    /// </para>
    /// <para>
    /// <see cref="IProgress{T}.Report"/> is called on the platform's callback thread. Marshal to the
    /// UI thread yourself if you need to update UI from the handler (e.g. wrap with
    /// <c>new Progress&lt;NearbyTransferProgress&gt;(update => MainThread.BeginInvokeOnMainThread(() => …))</c>).
    /// </para>
    /// </remarks>
    public IProgress<NearbyTransferProgress>? InboundProgress { get; set; }

    /// <summary>
    /// Returns an async stream of payloads received from the remote device, in arrival order.
    /// The enumerable completes when the peer disconnects or <see cref="DisposeAsync"/> is called.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>The loop body is the backpressure seam.</strong> The next payload is not dequeued
    /// until the body of your <c>await foreach</c> completes, so a handler may <c>await</c> freely
    /// (decode a file, generate a thumbnail, hit a database) without losing payloads or reordering
    /// them. This is the reason payloads are a stream rather than an event: an
    /// <see cref="EventHandler{TEventArgs}"/> returns <see langword="void"/> and cannot express
    /// "finish handling this one before delivering the next."
    /// </para>
    /// <para>
    /// <strong>Single consumer per connection.</strong> The receive channel is a data pipe, not a
    /// broadcast: items read by one enumerator are permanently removed. Calling this a second time
    /// (including after cancellation) throws <see cref="InvalidOperationException"/>. If several
    /// components need inbound data, consume once here and fan out a domain-level message of your
    /// own — see <c>docs/PAYLOAD-DELIVERY.md</c>.
    /// </para>
    /// <para>
    /// Payloads arrive on a platform background thread. Marshal to the UI thread inside the loop if
    /// you update UI from it.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">
    /// A token to cancel enumeration. Defaults to <see cref="CancellationToken.None"/>; pass
    /// <see cref="DisconnectedToken"/> to end the loop automatically when the peer disconnects.
    /// </param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyPayload"/> items.</returns>
    /// <exception cref="InvalidOperationException">Thrown if called more than once.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    public IAsyncEnumerable<NearbyPayload> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _receiveGuard, 1) != 0)
        {
            throw new InvalidOperationException(
                $"{nameof(ReceiveAsync)} may only be called once per connection, because payloads " +
                "read by one enumerator are permanently removed from the channel. If multiple " +
                "components need inbound data, consume the stream once and fan out your own " +
                "application-level message from inside the loop.");
        }

        return _receiveChannel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Disconnects from the remote device and releases all resources used by this connection.
    /// After disposal the receive stream completes and no further sends are possible.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the disconnect is signaled.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        await _disposeFactory();
        CompleteReceive();

        // _disconnectedCts is deliberately NOT disposed. DisconnectedToken is public and consumers
        // hold connection references past teardown (a page ViewModel checking why its loop ended);
        // disposing the source makes every subsequent read throw ObjectDisposedException. A
        // CancellationTokenSource with no timer and no registrations left holds nothing that needs
        // releasing once it has been cancelled.
    }

    /// <summary>
    /// Completes the receive channel, signaling that no more payloads will arrive.
    /// Called by the platform when the remote peer disconnects.
    /// </summary>
    internal void CompleteReceive()
    {
        _disconnectedTcs.TrySetResult();

        // Completing the writer is what ends ReceiveAsync: ReadAllAsync drains whatever is still
        // buffered and then finishes the loop normally. Payloads that arrived immediately before the
        // disconnect are therefore delivered, not dropped — the guarantee
        // PayloadWrittenBeforeDisconnect_IsNotLost exists to protect. DisconnectedToken must never
        // be used to drive that loop, for exactly this reason; see its remarks.
        _receiveChannel.Writer.TryComplete();

        _disconnectedCts.Cancel();
    }

    /// <summary>
    /// Writes an incoming payload to the receive channel.
    /// A <see langword="false"/> return (channel already completed) is silently dropped.
    /// </summary>
    internal void TryWritePayload(NearbyPayload payload)
        => _receiveChannel.Writer.TryWrite(payload);
}
