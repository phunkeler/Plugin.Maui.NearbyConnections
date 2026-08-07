using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents an established peer-to-peer connection with a remote device, over which payloads can
/// be sent and received.
/// </summary>
/// <remarks>
/// <para>
/// Obtain a connection by calling
/// <see cref="INearbySession.ConnectAsync(NearbyDevice, CancellationToken)"/> or
/// <see cref="INearbySession.AcceptAsync(NearbyDevice, CancellationToken)"/>, or by reading
/// <see cref="NearbyDevice.Connection"/>. The same instance is returned by all three.
/// </para>
/// <para>
/// Call <see cref="DisposeAsync"/> to disconnect from the remote device. Disposal is idempotent.
/// </para>
/// </remarks>
/// <seealso cref="INearbySession"/>
/// <seealso cref="NearbyDevice"/>
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
    /// Gets the remote device that this connection is established with.
    /// </summary>
    /// <value>The device on the other end of the connection.</value>
    public NearbyDevice RemoteDevice { get; }

    /// <summary>
    /// Gets the role that the local device plays in this connection.
    /// </summary>
    /// <value>
    /// <see cref="ConnectionRole.Initiator"/> if the local device called
    /// <see cref="INearbySession.ConnectAsync(NearbyDevice, CancellationToken)"/>;
    /// <see cref="ConnectionRole.Acceptor"/> if it called
    /// <see cref="INearbySession.AcceptAsync(NearbyDevice, CancellationToken)"/>.
    /// </value>
    public ConnectionRole Role { get; internal set; }

    /// <summary>
    /// Gets a task that completes when this connection terminates.
    /// </summary>
    /// <value>
    /// A <see cref="Task"/> that completes when the connection ends, from either side and for any
    /// reason.
    /// </value>
    /// <remarks>
    /// This task can be awaited concurrently with
    /// <see cref="ReceiveAsync(CancellationToken)"/> and does not consume the receive stream.
    /// </remarks>
    public Task Disconnected => _disconnectedTcs.Task;

    /// <summary>
    /// Gets a cancellation token that is canceled when this connection terminates.
    /// </summary>
    /// <value>
    /// A <see cref="CancellationToken"/> that is canceled when the connection ends, from either
    /// side and for any reason.
    /// </value>
    /// <remarks>
    /// <para>
    /// This is the token form of <see cref="Disconnected"/>. Use it to cancel your own
    /// per-connection work when the remote device goes away, such as a retry loop, a periodic
    /// keep-alive, or an upload started on the connection's behalf.
    /// </para>
    /// <para>
    /// <b>Do not pass this token to <see cref="ReceiveAsync(CancellationToken)"/>.</b> Doing so is
    /// unnecessary, because the receive stream already completes on disconnect. It is also harmful:
    /// cancellation is observed on every iteration, so a canceled token throws
    /// <see cref="OperationCanceledException"/> and discards payloads that were buffered
    /// immediately before the disconnect. Enumerate without a token, or with one of your own, and
    /// let the stream complete on its own.
    /// </para>
    /// <para>
    /// This property remains valid after <see cref="DisposeAsync"/> is called; reading it never
    /// throws <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example consumes payloads until the remote device disconnects.
    /// <code language="csharp">
    /// await foreach (var payload in connection.ReceiveAsync())
    /// {
    ///     await HandleAsync(payload);
    /// }
    /// </code>
    /// </example>
    public CancellationToken DisconnectedToken => _disconnectedCts.Token;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnection"/> class for use as a test
    /// double.
    /// </summary>
    /// <param name="remoteDevice">The remote device that this connection represents.</param>
    /// <param name="receiveChannel">
    /// The channel that delivers inbound payloads to
    /// <see cref="ReceiveAsync(CancellationToken)"/>.
    /// </param>
    /// <param name="sendBytesFactory">
    /// A delegate invoked when <see cref="SendAsync(byte[], CancellationToken)"/> is called.
    /// </param>
    /// <param name="sendFileFactory">
    /// A delegate invoked when any of the file-based <c>SendAsync</c> overloads is called.
    /// </param>
    /// <param name="disposeFactory">
    /// A delegate invoked when <see cref="DisposeAsync"/> is called.
    /// </param>
    /// <remarks>
    /// This constructor exists so that consumers can construct a connection in unit tests without
    /// a real platform session. Application code obtains connections from an
    /// <see cref="INearbySession"/> instead.
    /// </remarks>
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
    /// <param name="data">The bytes to send.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while sending.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation. The task completes
    /// when the bytes have been handed off to the platform.
    /// </returns>
    /// <remarks>
    /// On Android, byte payloads are limited to 32 KB. Use
    /// <see cref="SendAsync(string, IProgress{NearbyTransferProgress}?, CancellationToken)"/> for
    /// larger data.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public ValueTask SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return _sendBytesFactory(data, cancellationToken);
    }

    /// <summary>
    /// Sends the file identified by the specified URI to the remote device.
    /// </summary>
    /// <param name="fileUri">A URI that identifies the file to send.</param>
    /// <param name="progress">
    /// An optional provider that receives progress updates for the outgoing transfer, or
    /// <see langword="null"/> to ignore progress.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while transferring.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when the
    /// transfer has finished.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileUri"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    /// <exception cref="NearbyTransferTimeoutException">
    /// No transfer progress was reported within
    /// <see cref="NearbyConnectionsOptions.TransferInactivityTimeout"/>.
    /// </exception>
    public Task SendAsync(
        string fileUri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileUri);
        return _sendFileFactory(fileUri, progress, cancellationToken);
    }

    /// <summary>
    /// Sends the specified file to the remote device.
    /// </summary>
    /// <param name="fileResult">The file to send.</param>
    /// <param name="progress">
    /// An optional provider that receives progress updates for the outgoing transfer, or
    /// <see langword="null"/> to ignore progress.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while transferring.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when the
    /// transfer has finished.
    /// </returns>
    /// <remarks>
    /// This overload accepts the same <see cref="FileResult"/> type that
    /// <see cref="FilePayload"/> exposes, so a received file can be forwarded without conversion.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileResult"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    /// <exception cref="NearbyTransferTimeoutException">
    /// No transfer progress was reported within
    /// <see cref="NearbyConnectionsOptions.TransferInactivityTimeout"/>.
    /// </exception>
    public Task SendAsync(
        FileResult fileResult,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileResult);
        return _sendFileFactory(fileResult.FullPath, progress, cancellationToken);
    }

    /// <summary>
    /// Sends the specified payload to the remote device, selecting the appropriate transfer
    /// mechanism for the payload type.
    /// </summary>
    /// <param name="payload">
    /// The payload to send. Must be a <see cref="BytesPayload"/> or a <see cref="FilePayload"/>.
    /// </param>
    /// <param name="progress">
    /// An optional provider that receives progress updates for the outgoing transfer. This
    /// parameter is used only when <paramref name="payload"/> is a <see cref="FilePayload"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while sending.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation. The task completes
    /// when the payload has been handed off to the platform.
    /// </returns>
    /// <remarks>
    /// This overload accepts the same payload types that
    /// <see cref="ReceiveAsync(CancellationToken)"/> produces, so a received payload can be
    /// forwarded without conversion.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="payload"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="payload"/> is not a <see cref="BytesPayload"/> or a
    /// <see cref="FilePayload"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    /// <exception cref="NearbyTransferTimeoutException">
    /// <paramref name="payload"/> is a <see cref="FilePayload"/> and no transfer progress was
    /// reported within <see cref="NearbyConnectionsOptions.TransferInactivityTimeout"/>.
    /// </exception>
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
    /// Gets or sets the provider that receives progress updates for inbound file transfers.
    /// </summary>
    /// <value>
    /// A provider that receives progress updates for incoming transfers, or <see langword="null"/>
    /// to ignore inbound progress. The default is <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// Outbound progress is supplied for each call, as a parameter on the <c>SendAsync</c>
    /// overloads, because the caller starts the transfer and knows which provider to use. Inbound
    /// transfers cannot follow that pattern, because they begin on a platform callback thread
    /// before the consumer has any opportunity to supply a provider. Setting this property lets a
    /// handler be attached as soon as the connection is established, before any payload arrives.
    /// </para>
    /// <para>
    /// <see cref="IProgress{T}.Report(T)"/> is invoked on a platform callback thread. Marshal to
    /// the UI thread inside the handler if you update the user interface from it.
    /// </para>
    /// </remarks>
    public IProgress<NearbyTransferProgress>? InboundProgress { get; set; }

    /// <summary>
    /// Returns an asynchronous stream of the payloads received from the remote device, in arrival
    /// order.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while enumerating. Do not pass
    /// <see cref="DisconnectedToken"/>; see the remarks on that property.
    /// </param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyPayload"/> objects that completes
    /// when the remote device disconnects or <see cref="DisposeAsync"/> is called.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The body of the enumeration provides backpressure. The next payload is not dequeued until
    /// the body completes, so a handler can await long-running work — decoding a file, generating a
    /// thumbnail, writing to a database — without losing or reordering payloads.
    /// </para>
    /// <para>
    /// <b>A connection supports a single payload consumer.</b> The receive stream is a pipe rather
    /// than a broadcast, and payloads read by one enumerator are permanently removed. Calling this
    /// method more than once, including after cancellation, throws
    /// <see cref="InvalidOperationException"/>. If several components need inbound data, enumerate
    /// once and distribute an application-level message from inside the loop.
    /// </para>
    /// <para>
    /// Payloads are delivered on a platform background thread. Marshal to the UI thread inside the
    /// loop if you update the user interface from it.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This method has already been called on this connection.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
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
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous dispose operation. The task
    /// completes when the disconnect has been signaled to the platform.
    /// </returns>
    /// <remarks>
    /// After disposal, the stream returned by <see cref="ReceiveAsync(CancellationToken)"/>
    /// completes and no further payloads can be sent. Calling this method more than once performs
    /// no additional work.
    /// </remarks>
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
    /// Whether anything has started consuming this connection's payloads via
    /// <see cref="ReceiveAsync"/>.
    /// </summary>
    /// <remarks>
    /// Used to detect the silent-loss case: payloads arriving on a connection nobody reads are
    /// buffered forever in an unbounded channel and never observed. See
    /// <c>NearbySession.WarnIfPayloadUnobserved</c>.
    /// </remarks>
    internal bool IsBeingConsumed => Volatile.Read(ref _receiveGuard) != 0;

    /// <summary>
    /// Writes an incoming payload to the receive channel.
    /// A <see langword="false"/> return (channel already completed) is silently dropped.
    /// </summary>
    internal void TryWritePayload(NearbyPayload payload)
        => _receiveChannel.Writer.TryWrite(payload);
}
