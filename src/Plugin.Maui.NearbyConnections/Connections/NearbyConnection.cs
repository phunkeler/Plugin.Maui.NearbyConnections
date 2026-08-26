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
/// <see cref="NearbyConnectionRequest.AcceptAsync(CancellationToken)"/>, or
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
    readonly IPlatformConnection _platform;
    readonly TaskCompletionSource<NearbyEndReason> _disconnectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly CancellationTokenSource _disconnectedCts = new();

    int _disposeGuard;
    int _receiveGuard;

    internal bool IsBeingConsumed => Volatile.Read(ref _receiveGuard) != 0;

    /// <summary>
    /// Gets the remote device this connection is established with.
    /// </summary>
    /// <value>The device on the other end of the connection.</value>
    public NearbyDevice RemoteDevice { get; }

    /// <summary>
    /// Gets a task that completes once this connection terminates, carrying why it ended.
    /// </summary>
    /// <value>
    /// A task that completes when the connection ends, from either side and for any reason. Its
    /// result is the locally-observed reason: <see cref="NearbyEndReason.DisconnectedByLocal"/>
    /// for a local disconnect, <see cref="NearbyEndReason.SessionStopped"/> when the session
    /// stopped, and <see cref="NearbyEndReason.Disconnected"/> when the remote device closed the
    /// connection or the link was lost — the platforms cannot tell those two apart.
    /// </value>
    /// <remarks>
    /// Safe to await alongside <see cref="ReceiveAsync(CancellationToken)"/> — it does not consume
    /// the receive stream. <b>Never faults or is canceled:</b> a dropped connection is not an error
    /// at this boundary, so this task always completes successfully. Continuations are queued to
    /// the thread pool, never run inline on the platform callback that ended the connection —
    /// contrast <see cref="DisconnectedToken"/>, whose registrations do run inline.
    /// </remarks>
    public Task<NearbyEndReason> Disconnected => _disconnectedTcs.Task;

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
    /// <b>Registered callbacks run inline on the thread that observed the disconnect</b> — the
    /// platform SDK's own callback thread, or the thread that called <see cref="DisposeAsync"/>.
    /// This differs from awaiting <see cref="Disconnected"/>, whose continuations are queued to the
    /// thread pool. Keep a registration short, and do not let it throw: a slow registration stalls
    /// the platform's own callback dispatch, and a thrown exception surfaces on the thread that
    /// ended the connection, not on yours.
    /// </para>
    /// <para>
    /// <b>Remains valid for the lifetime of this object, including after
    /// <see cref="DisposeAsync"/>.</b> Reading the token, reading
    /// <see cref="CancellationToken.IsCancellationRequested"/>, and calling
    /// <see cref="CancellationToken.Register(Action)"/> all keep working and never throw
    /// <see cref="ObjectDisposedException"/> — hold a connection reference past teardown to ask why
    /// a loop ended. A registration added after the connection ended runs immediately, inline on
    /// the calling thread. The token also stays usable as an input to
    /// <see cref="CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, CancellationToken)"/>
    /// at any point in the connection's life.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example stops the application's own per-connection work when the remote
    /// device goes away, without waiting for the caller's own token.
    /// <code language="csharp">
    /// using var linked = CancellationTokenSource.CreateLinkedTokenSource(
    ///     connection.DisconnectedToken, cancellationToken);
    ///
    /// await SyncProfilePhotoAsync(connection.RemoteDevice, linked.Token);
    /// </code>
    /// </example>
    public CancellationToken DisconnectedToken => _disconnectedCts.Token;

    internal NearbyConnection(
        NearbyDevice remoteDevice,
        Channel<NearbyPayload> receiveChannel,
        IPlatformConnection platform)
    {
        RemoteDevice = remoteDevice;
        _receiveChannel = receiveChannel;
        _platform = platform;
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
        return _platform.SendBytesAsync(data, cancellationToken);
    }

    /// <summary>
    /// Sends the file at the specified URI to the remote device.
    /// </summary>
    /// <param name="fileUri">A URI that identifies the file to send.</param>
    /// <param name="progress">
    /// An optional provider that receives progress updates for the outgoing transfer, or
    /// <see langword="null"/> to ignore progress. <see cref="IProgress{T}.Report(T)"/> is invoked
    /// directly on the platform SDK's callback thread — the same contract as
    /// <see cref="InboundProgress"/> — so marshal to the UI thread inside the handler if it updates
    /// the user interface, and keep the handler short. A <see cref="Progress{T}"/> instance
    /// marshals for you, because it captures the synchronization context it was constructed on.
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
        return _platform.SendFileAsync(fileUri, progress, cancellationToken);
    }

    /// <summary>
    /// Opens a named one-way byte stream to the remote device, for unknown-length or live data.
    /// The remote side receives a <see cref="NearbyStreamPayload"/> — the readable half plus this
    /// name — through its own <see cref="ReceiveAsync(CancellationToken)"/> loop.
    /// </summary>
    /// <param name="name">
    /// The stream's name, shown to the remote application. At most 1024 UTF-8 bytes.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while the stream opens.
    /// </param>
    /// <returns>
    /// The writable stream. Dispose it to end the stream — the remote reader then observes the
    /// end of its half. A dropped connection ends the stream from either side.
    /// </returns>
    /// <remarks>
    /// Both platforms carry this natively — a stream payload on Android, a named
    /// MultipeerConnectivity stream on iOS — and the name arrives with the stream on both.
    /// Writes are not bounded by <see cref="NearbyOptions.TransferInactivityTimeout"/>: the
    /// application owns the stream's lifetime and pacing.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> exceeds 1024 UTF-8 bytes.</exception>
    /// <exception cref="NearbyException">
    /// The remote device disconnected before this call, so no active connection remains.
    /// </exception>
    /// <exception cref="NearbyTransferException">The platform rejected opening the stream.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public Task<Stream> OpenStreamAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _platform.OpenStreamAsync(name, cancellationToken);
    }

    /// <summary>
    /// Sends the specified file to the remote device.
    /// </summary>
    /// <param name="file">The file to send.</param>
    /// <param name="progress">
    /// An optional provider that receives progress updates for the outgoing transfer, or
    /// <see langword="null"/> to ignore progress. <see cref="IProgress{T}.Report(T)"/> is invoked
    /// directly on the platform SDK's callback thread — the same contract as
    /// <see cref="InboundProgress"/> — so marshal to the UI thread inside the handler if it updates
    /// the user interface, and keep the handler short. A <see cref="Progress{T}"/> instance
    /// marshals for you, because it captures the synchronization context it was constructed on.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while transferring.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes once the
    /// transfer finishes.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Accepts the same <see cref="FileResult"/> type that <see cref="NearbyFilePayload"/> exposes,
    /// so a received file can be forwarded without conversion.
    /// </para>
    /// <para>
    /// A <see cref="FileResult"/> from a picker does not always carry a readable file-system path —
    /// on iOS <c>FullPath</c> may be a bare file name. When that is the case, the file is copied to
    /// a temporary location through its stream, sent from there, and the temporary copy is deleted
    /// once the transfer ends. Pass a picker result directly; no staging of your own is needed.
    /// </para>
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

        // A file the library staged, or any result whose path is real, sends straight from disk.
        if (File.Exists(file.FullPath))
        {
            return _platform.SendFileAsync(file.FullPath, progress, cancellationToken);
        }

        return SendStagedAsync();

        async Task SendStagedAsync()
        {
            var staged = await StageToTempAsync(
                file.OpenReadAsync,
                file.FileName,
                cancellationToken).ConfigureAwait(false);

            try
            {
                await _platform.SendFileAsync(staged, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    Directory.Delete(Path.GetDirectoryName(staged)!, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Never widen this catch — a failure to send must still surface.
                }
            }
        }
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
    /// the transfer and already knows which provider to use. An inbound transfer cannot follow that
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
    /// completes once the disconnect is signaled to the platform and any in-flight inbound work for
    /// this connection has drained or timed out.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The stream from <see cref="ReceiveAsync(CancellationToken)"/> ends as soon as teardown
    /// starts, before this task completes, and no further payloads can be sent. Idempotent - a
    /// second call performs no additional work. A failure signaling the disconnect to the platform
    /// propagates from this method unguarded, and teardown is not retried, so a caller that wants
    /// disposal to always succeed should catch around the call.
    /// </para>
    /// <para>
    /// <b>Android waits for in-flight inbound work; iOS does not need to.</b> On Android, an inbound
    /// file payload copies asynchronously, so this call waits (bounded, a few seconds) for that copy
    /// to finish before the platform handles it depends on are freed. If the wait times out, the
    /// copy may fail and the platform logs a warning rather than throwing here — the file staged for
    /// that payload may be left partly written. On iOS, the inbound path copies synchronously on the
    /// delegate queue, so there is never work left to wait for. This wait covers inbound work only —
    /// a pending outgoing <c>SendAsync</c> call observes the disconnect through its own transfer
    /// completion, separately from this wait.
    /// </para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        // Record the local reason before the platform release runs: the release path completes
        // the same source with Disconnected, and the first completion wins.
        _disconnectedTcs.TrySetResult(DisposeReason);

        await _platform.DisposeAsync().ConfigureAwait(false);
        CompleteReceive();

        // _disconnectedCts is deliberately NOT disposed: the remarks on DisconnectedToken promise
        // the token stays readable and registerable after teardown, and disposing the source makes
        // both throw ObjectDisposedException. NearbyConnectionTests.DisconnectedToken
        // .RemainsReadable_AfterDisposeAsync pins that contract.
        //
        // Nothing leaks. This source is constructed with no delay, so it never allocated a Timer,
        // and Cancel() clears the registration list — what remains is a managed object with no
        // finalizer, collected with this connection. The one caveat: reading Token.WaitHandle would
        // lazily allocate a ManualResetEvent that only Dispose releases. Nothing here does, and a
        // consumer would have to reach for WaitHandle deliberately.
    }

    /// <summary>
    /// The reason a local disposal reports through <see cref="Disconnected"/>. Defaults to
    /// <see cref="NearbyEndReason.DisconnectedByLocal"/>; session teardown sets
    /// <see cref="NearbyEndReason.SessionStopped"/> before it disposes the connection.
    /// </summary>
    internal NearbyEndReason DisposeReason { get; set; } = NearbyEndReason.DisconnectedByLocal;

    internal void CompleteReceive() => CompleteReceive(NearbyEndReason.Disconnected);

    internal void CompleteReceive(NearbyEndReason reason)
    {
        _disconnectedTcs.TrySetResult(reason);
        _receiveChannel.Writer.TryComplete();
        _disconnectedCts.Cancel();
    }

    internal void TryWritePayload(NearbyPayload payload)
        => _receiveChannel.Writer.TryWrite(payload);

    internal static async Task<string> StageToTempAsync(
        Func<Task<Stream>> openRead,
        string fileName,
        CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("nearby-send-").FullName;
        var path = Path.Combine(directory, Path.GetFileName(fileName));
        var source = await openRead().ConfigureAwait(false);

        await using (source.ConfigureAwait(false))
        {
            var destination = File.Create(path);

            await using (destination.ConfigureAwait(false))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }
        }

        return path;
    }
}