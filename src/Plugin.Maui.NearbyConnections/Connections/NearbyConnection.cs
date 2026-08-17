using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents an established connection to a remote device, over which payloads can be sent and
/// received.
/// </summary>
/// <remarks>
/// <para>
/// Obtain an instance from
/// <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/>,
/// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/>, or
/// <see cref="INearby.TryGetConnection(string, out NearbyConnection)"/> — all three return the same
/// instance for a given remote device.
/// </para>
/// <para>
/// Call <see cref="DisposeAsync"/> to disconnect. Disposal is idempotent.
/// </para>
/// </remarks>
/// <seealso cref="INearby"/>
/// <seealso cref="NearbyDevice"/>
public sealed class NearbyConnection : IAsyncDisposable
{
    readonly Channel<NearbyPayload> _receiveChannel;
    readonly Func<byte[], CancellationToken, Task> _sendBytes;
    readonly Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task> _sendFile;
    readonly Func<ValueTask> _dispose;
    readonly TaskCompletionSource _disconnectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly CancellationTokenSource _disconnectedCts = new();

    int _disposeGuard;
    int _receiveGuard;

    /// <summary>
    /// Gets the remote device this connection is established with.
    /// </summary>
    /// <value>The device on the other end of the connection.</value>
    public NearbyDevice RemoteDevice { get; }

    /// <summary>
    /// Gets a task that completes once this connection terminates.
    /// </summary>
    /// <value>
    /// A <see cref="Task"/> that completes when the connection ends, from either side and for any
    /// reason.
    /// </value>
    /// <remarks>
    /// Safe to await alongside <see cref="ReceiveAsync(CancellationToken)"/> — it does not consume
    /// the receive stream. <b>Never faults or is canceled:</b> a dropped connection is not an error
    /// at this boundary, so this task always completes successfully and carries no information
    /// about why the connection ended.
    /// </remarks>
    public Task Disconnected => _disconnectedTcs.Task;

    /// <summary>
    /// Gets a cancellation token that is canceled once this connection terminates.
    /// </summary>
    /// <value>
    /// A <see cref="CancellationToken"/> that is canceled when the connection ends, from either
    /// side and for any reason.
    /// </value>
    /// <remarks>
    /// <para>
    /// The token form of <see cref="Disconnected"/>. Use it to cancel your own per-connection work
    /// when the remote device goes away — a retry loop, a periodic keep-alive, an upload started on
    /// the connection's behalf.
    /// </para>
    /// <para>
    /// <b>Do not pass this token to <see cref="ReceiveAsync(CancellationToken)"/>.</b> It is
    /// unnecessary, since the receive stream already completes on disconnect — and harmful, since
    /// cancellation is observed on every iteration, so a canceled token throws
    /// <see cref="OperationCanceledException"/> and discards payloads buffered immediately before
    /// the disconnect. Enumerate with no token, or one of your own, and let the stream complete on
    /// its own.
    /// </para>
    /// <para>
    /// Remains valid after <see cref="DisposeAsync"/> — reading it never throws
    /// <see cref="ObjectDisposedException"/>.
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

    internal NearbyConnection(
        NearbyDevice remoteDevice,
        Channel<NearbyPayload> receiveChannel,
        Func<byte[], CancellationToken, Task> sendBytes,
        Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task> sendFile,
        Func<ValueTask> dispose)
    {
        RemoteDevice = remoteDevice;
        _receiveChannel = receiveChannel;
        _sendBytes = sendBytes;
        _sendFile = sendFile;
        _dispose = dispose;
    }

    /// <summary>
    /// Sends raw bytes to the remote device.
    /// </summary>
    /// <param name="data">The bytes to send.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while sending.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes once the
    /// bytes are handed off to the platform.
    /// </returns>
    /// <remarks>
    /// Android limits byte payloads to 32 KB. Use
    /// <see cref="SendAsync(string, IProgress{NearbyTransferProgress}?, CancellationToken)"/> for
    /// larger data.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    /// <exception cref="NearbyTransferException">
    /// The platform rejected the send.
    /// </exception>
    /// <exception cref="NearbyException">
    /// The remote device disconnected before this call, so no active connection remains to send on.
    /// </exception>
    public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return _sendBytes(data, cancellationToken);
    }

    /// <summary>
    /// Sends the file at the specified URI to the remote device.
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
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes once the
    /// transfer finishes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileUri"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    /// <exception cref="NearbyTransferTimeoutException">
    /// No transfer progress was reported within
    /// <see cref="NearbyOptions.TransferInactivityTimeout"/>.
    /// </exception>
    /// <exception cref="NearbyTransferException">
    /// The file could not be sent — for example, the URI does not identify a readable file, or the
    /// platform rejected the send.
    /// </exception>
    /// <exception cref="NearbyException">
    /// The remote device disconnected before this call, so no active connection remains to send on.
    /// </exception>
    public Task SendAsync(
        string fileUri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileUri);
        return _sendFile(fileUri, progress, cancellationToken);
    }

    /// <summary>
    /// Sends the specified file to the remote device.
    /// </summary>
    /// <param name="file">The file to send.</param>
    /// <param name="progress">
    /// An optional provider that receives progress updates for the outgoing transfer, or
    /// <see langword="null"/> to ignore progress.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while transferring.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes once the
    /// transfer finishes.
    /// </returns>
    /// <remarks>
    /// Accepts the same <see cref="FileResult"/> type that <see cref="NearbyFilePayload"/> exposes,
    /// so a received file can be forwarded without conversion.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="file"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    /// <exception cref="NearbyTransferTimeoutException">
    /// No transfer progress was reported within
    /// <see cref="NearbyOptions.TransferInactivityTimeout"/>.
    /// </exception>
    /// <exception cref="NearbyTransferException">
    /// The platform rejected the send.
    /// </exception>
    /// <exception cref="NearbyException">
    /// The remote device disconnected before this call, so no active connection remains to send on.
    /// </exception>
    public Task SendAsync(
        FileResult file,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        return _sendFile(file.FullPath, progress, cancellationToken);
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
    /// Outbound progress is a parameter on each <c>SendAsync</c> overload, because the caller starts
    /// the transfer and already knows which provider to use. An inbound transfer can't follow that
    /// pattern — it begins on a platform callback thread before the consumer has any chance to
    /// supply a provider. Setting this property instead lets a handler be attached as soon as the
    /// connection is established, ahead of any payload arriving.
    /// </para>
    /// <para>
    /// <see cref="IProgress{T}.Report(T)"/> is invoked directly on the platform SDK's callback
    /// thread — marshal to the UI thread inside the handler if it updates the user interface, and
    /// keep the handler short, because it runs inline on the SDK's own dispatch. This differs from
    /// <see cref="ReceiveAsync(CancellationToken)"/> and
    /// <see cref="INearbyDevices.Changes"/>, which are pumped through a channel and therefore arrive
    /// on a thread-pool thread.
    /// </para>
    /// </remarks>
    public IProgress<NearbyTransferProgress>? InboundProgress { get; set; }

    /// <summary>
    /// Returns an asynchronous stream of the payloads received from the remote device, in arrival
    /// order.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while enumerating. Do not pass
    /// <see cref="DisconnectedToken"/> — see the remarks on that property.
    /// </param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyPayload"/> objects that completes
    /// when the remote device disconnects or <see cref="DisposeAsync"/> is called.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The enumeration body provides backpressure: the next payload is not dequeued until the
    /// current body completes, so a handler can await long-running work — decoding a file,
    /// generating a thumbnail, writing to a database — without losing or reordering payloads.
    /// </para>
    /// <para>
    /// <b>Single-consumer.</b> The receive stream is a pipe, not a broadcast — a payload read by one
    /// enumerator is permanently removed from it. Calling this method a second time, including after
    /// the first enumeration was cancelled, throws <see cref="InvalidOperationException"/>. Where
    /// several components need inbound data, enumerate once and fan out an application-level message
    /// from inside the loop.
    /// </para>
    /// <para>
    /// Payloads are delivered on a thread-pool thread, never the UI thread — marshal to the UI
    /// thread inside the loop if it updates the user interface. This is not the platform SDK's own
    /// callback thread: the callback writes the payload into a channel, and the enumeration resumes
    /// on the reading side of that boundary.
    /// </para>
    /// <para>
    /// <b>Never faults.</b> A disconnect — local, remote, or platform-initiated — ends the
    /// enumeration cleanly, indistinguishable from an orderly shutdown. The only exceptions the
    /// enumeration itself can produce are <see cref="OperationCanceledException"/> from
    /// <paramref name="cancellationToken"/>, and the once-only
    /// <see cref="InvalidOperationException"/> from a second call. Use <see cref="Disconnected"/> to
    /// learn that the connection ended.
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
    /// completes once the disconnect is signaled to the platform.
    /// </returns>
    /// <remarks>
    /// Once this completes, the stream from <see cref="ReceiveAsync(CancellationToken)"/> ends and
    /// no further payloads can be sent. Idempotent - a second call performs no additional work. A
    /// failure signaling the disconnect to the platform propagates from this method unguarded, and
    /// teardown is not retried, so a caller that wants disposal to always succeed should catch
    /// around the call.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        await _dispose();
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
    /// <c>PlatformNearby.WritePayload</c>, which logs when a payload arrives unobserved.
    /// </remarks>
    internal bool IsBeingConsumed => Volatile.Read(ref _receiveGuard) != 0;

    /// <summary>
    /// Writes an incoming payload to the receive channel.
    /// A <see langword="false"/> return (channel already completed) is silently dropped.
    /// </summary>
    internal void TryWritePayload(NearbyPayload payload)
        => _receiveChannel.Writer.TryWrite(payload);
}