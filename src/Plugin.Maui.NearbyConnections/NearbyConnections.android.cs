namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    const uint StreamNameMagic = 0x504D4E43;

    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, IncomingStreamTransfer> _incomingStreams = [];
    readonly ConcurrentDictionary<long, OutgoingStreamTransfer> _outgoingStreams = [];

    #region Discovery

    public void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        Trace.TraceInformation($"Endpoint found: EndpointId={endpointId}, EndpointName={info.EndpointName}");
        var device = _deviceManager.DeviceFound(endpointId, info.EndpointName);
        Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    public void OnEndpointLost(string endpointId)
    {
        Trace.TraceInformation($"Endpoint lost: EndpointId={endpointId}");

        if (_deviceManager.TryGetDevice(endpointId, out var existingDevice)
            && existingDevice.State == NearbyDeviceState.Connected)
        {
            return;
        }

        var device = _deviceManager.DeviceLost(endpointId);
        if (device is not null)
        {
            Events.OnDeviceLost(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Discovery

    #region Advertising

    /// <summary>
    /// "A basic encrypted channel has been created between you and the endpoint.
    /// Both sides are now asked if they wish to accept or reject the connection before any data can be sent over this channel."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectioninitiated-string-endpointid,-connectioninfo-connectioninfo">developers.google.com</see>
    /// </summary>
    /// <param name="endpointId">The identifier for the remote endpoint.</param>
    /// <param name="connectionInfo">Other relevant information about the connection.</param>
    public async void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        Trace.TraceInformation($"Connection initiated: EndpointId={endpointId}, EndpointName={connectionInfo.EndpointName}, IsIncomingConnection={connectionInfo.IsIncomingConnection}");

        var state = connectionInfo.IsIncomingConnection
            ? NearbyDeviceState.ConnectionRequestedInbound
            : NearbyDeviceState.ConnectionRequestedOutbound;

        var device = _deviceManager.SetState(endpointId, state)
            ?? _deviceManager.GetOrAddDevice(endpointId, connectionInfo.EndpointName, state);

        if (connectionInfo.IsIncomingConnection)
        {
            Events.OnConnectionRequested(device, TimeProvider.GetUtcNow());

            if (Options.AutoAcceptConnections)
            {
                await PlatformRespondToConnectionAsync(device, accept: true);
            }
        }
        else
        {
            // Skip this extra step
            await PlatformRespondToConnectionAsync(device, accept: true);
        }
    }

    /// <summary>
    /// "Called after both sides have either accepted or rejected the connection.
    /// If the ConnectionResolution's status is CommonStatusCodes.SUCCESS, both sides have
    /// accepted the connection and may now send Payloads to each other. Otherwise, the connection was rejected."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectionresult-string-endpointid,-connectionresolution-resolution">developers.google.com</see>
    /// </summary>
    /// <param name="endpointId">The identifier for the remote endpoint</param>
    /// <param name="resolution">The final result after tallying both devices' accept/reject responses</param>
    public void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        Trace.TraceInformation($"Connection result: EndpointId={endpointId}, StatusCode={resolution.Status.StatusCode}, StatusMessage={resolution.Status.StatusMessage ?? string.Empty}, IsSuccess={resolution.Status.IsSuccess}");

        if (resolution.Status.IsSuccess)
        {
            var device = _deviceManager.SetState(endpointId, NearbyDeviceState.Connected);

            if (device is not null)
            {
                Events.OnConnectionResponded(device, TimeProvider.GetUtcNow(), true);
            }
        }
        else
        {
            var device = _deviceManager.SetState(endpointId, NearbyDeviceState.Discovered);

            if (device is not null)
            {
                Events.OnConnectionResponded(device, TimeProvider.GetUtcNow(), false);
            }
        }
    }

    /// <summary>
    /// "Called when a remote endpoint is disconnected or has become unreachable."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-ondisconnected-string-endpointid">developers.google.com</see>
    /// </summary>
    /// <param name="endpointId"></param>
    public void OnDisconnected(string endpointId)
    {
        Trace.TraceInformation($"Disconnected from EndpointId={endpointId}");

        var device = _deviceManager.DeviceDisconnected(endpointId);

        if (device is not null)
        {
            Events.OnDeviceDisconnected(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Advertising

    void OnPayloadReceived(string endpointId, Payload payload)
    {
        Trace.TraceInformation($"Payload received: EndpointId={endpointId}, PayloadId={payload.Id}, PayloadType={payload.PayloadType}");

        if (TryHandleStreamNameMetadata(payload))
        {
            return;
        }

        _incomingPayloads.TryAdd(payload.Id, (endpointId, payload));

        if (payload.PayloadType == Payload.Type.Stream)
        {
            BeginDrainingStreamPayload(payload);
        }
    }

    // A bytes payload beginning with StreamNameMagic is a stream-name metadata packet sent
    // by PlaformSendAsync just before the stream payload. The format is:
    // [ magic (4 bytes) | payloadId (8 bytes LE) | name (UTF-8) ]
    bool TryHandleStreamNameMetadata(Payload payload)
    {
        if (payload.PayloadType != Payload.Type.Bytes
            || payload.AsBytes() is not { Length: > 12 } meta
            || BinaryPrimitives.ReadUInt32LittleEndian(meta) != StreamNameMagic)
        {
            return false;
        }

        var streamPayloadId = BinaryPrimitives.ReadInt64LittleEndian(meta.AsSpan(4));
        var name = Encoding.UTF8.GetString(meta, 12, meta.Length - 12);

        // Store the name on the transfer if it alreay exists, or create a placeholder so
        // the name is available regardless of which packet (metadata vs. stream) arrives first.
        _incomingStreams.AddOrUpdate(
            streamPayloadId,
            addValue: new IncomingStreamTransfer(Name: name),
            updateValueFactory: (_, existing) => existing with { Name = name });

        Trace.TraceInformation($"Stream name metadata received: PayloadId={streamPayloadId}, Name={name}");
        return true;
    }

    void BeginDrainingStreamPayload(Payload payload)
    {
        var drainTask = Task.Run(() => DrainStreamPayload(payload.AsStream()));

        _incomingStreams.AddOrUpdate(
            payload.Id,
            addValue: new IncomingStreamTransfer(DrainTask: drainTask),
            updateValueFactory: (_, existing) => existing with { DrainTask = drainTask });
    }

    async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        Trace.TraceInformation($"Payload transfer update: EndpointId={endpointId}, PayloadId={update.PayloadId}, TransferStatus={update.TransferStatus}, TotalBytes={update.TotalBytes}, BytesTransferred={update.BytesTransferred}");

        var status = ToNearbyTransferStatus(update.TransferStatus);

        if (_outgoingStreams.TryGetValue(update.PayloadId, out var transfer))
        {
            transfer.ReportProgress(new NearbyTransferProgress(
                payloadId: update.PayloadId,
                bytesTransferred: update.BytesTransferred,
                totalBytes: update.TotalBytes,
                status));
        }

        if (!_deviceManager.TryGetDevice(endpointId, out var device))
        {
            return;
        }

        Events.OnIncomingTransferProgress(
            device,
            new NearbyTransferProgress(update.PayloadId, update.BytesTransferred, update.TotalBytes, status),
            TimeProvider.GetUtcNow());


        if (update.TransferStatus == PayloadTransferUpdate.Status.Success)
        {
            await OnIncomingPayloadSuccess(device, update.PayloadId);

        }
    }

    async Task OnIncomingPayloadSuccess(NearbyDevice device, long payloadId)
    {
        if (!_incomingPayloads.TryRemove(payloadId, out var entry))
        {
            return;
        }

        NearbyPayload? nearbyPayload;

        if (entry.Payload.PayloadType == Payload.Type.Stream
            && _incomingStreams.TryRemove(payloadId, out var incomingStream)
            && incomingStream.DrainTask is not null)
        {
            var drained = await incomingStream.DrainTask;

            nearbyPayload = drained is not null
                ? new StreamPayload(drained.StreamFactory, incomingStream.Name)
                : null;
        }
        else
        {
            nearbyPayload = entry.Payload.AsBytes() is { } bytes
                ? new BytesPayload(bytes)
                : null;
        }

        if (nearbyPayload is not null)
        {
            Events.OnDataReceived(device, nearbyPayload, TimeProvider.GetUtcNow());
        }

        entry.Payload.Dispose();
    }

    static StreamPayload? DrainStreamPayload(Payload.Stream? payloadStream)
    {
        var inputStream = payloadStream?.AsInputStream();

        if (inputStream is null)
        {
            return null;
        }

        // Drain the Java InputStream into a MemoryStream synchronously (we are already on a
        // background callback thread) so the caller gets a replayable Stream factory.
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        int read;
        while ((read = inputStream.Read(buffer)) > 0)
        {
            ms.Write(buffer, 0, read);
        }

        var captured = ms.ToArray();
        return new StreamPayload(() => new MemoryStream(captured));
    }

    Task PlatformRequestConnectionAsync(NearbyDevice device)
    {
        _deviceManager.SetState(device.Id, NearbyDeviceState.ConnectionRequestedOutbound);

        return NearbyClass
            .GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
            .RequestConnectionAsync(
                Options.DisplayName,
                device.Id,
                new ConnectionRequestCallback(
                    OnConnectionInitiated,
                    OnConnectionResult,
                    OnDisconnected));
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return accept
            ? client.AcceptConnectionAsync(device.Id, new ConnectionCallback(OnPayloadReceived, OnPayloadTransferUpdate))
            : client.RejectConnectionAsync(device.Id);
    }

    static async Task PlatformSendAsync(
        NearbyDevice device,
        byte[] data,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var payload = Payload.FromBytes(data);

        await client.SendPayloadAsync(device.Id, payload);

        // Bytes payloads complete synchronously on enqueue; report success immediately
        progress?.Report(new NearbyTransferProgress(
            payloadId: payload.Id,
            bytesTransferred: data.Length,
            totalBytes: data.Length,
            NearbyTransferStatus.Success));
    }

    async Task PlatformSendAsync(
        NearbyDevice device,
        FileResult fileResult,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var stream = await fileResult.OpenReadAsync();
        var payload = Payload.FromStream(stream);

        // Send a small metadata bytes payload first so the receiver can associate the stream name
        // with this payload id.
        await client.SendPayloadAsync(device.Id, BuildStreamNameBytePayload(payload.Id, fileResult.FileName));

        var transfer = new OutgoingStreamTransfer(payload, stream, progress, Options.TransferInactivityTimeout);

        _outgoingStreams[payload.Id] = transfer;

        try
        {
            await client.SendPayloadAsync(device.Id, payload);

            // SendPayloadAsync only enqueues — await the actual transfer completion.
            // Link the caller's token with the inactivity token so either can abort the transfer.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(() => _ = client.CancelPayloadAsync(payload.Id));
            await transfer.Completion.WaitAsync(linkedCts.Token);
        }
        finally
        {
            _outgoingStreams.TryRemove(payload.Id, out _);
            transfer.Dispose();
        }
    }

    /// <summary>
    /// Holds the drain task and optional name for an in-flight incoming stream payload.
    /// </summary>
    /// <param name="DrainTask"></param>
    /// <param name="Name"></param>
    sealed record IncomingStreamTransfer(Task<StreamPayload?>? DrainTask = null, string Name = "");

    /// <summary>
    /// Holds all state for an in-flight outgoing stream transfer so it can be
    /// awaited and cleaned up in a single place.
    /// </summary>
    sealed class OutgoingStreamTransfer(
        Payload payload,
        Stream stream,
        IProgress<NearbyTransferProgress>? progress,
        TimeSpan inactivityTimeout) : IDisposable
    {
        readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource _inactivityCts = new(inactivityTimeout);

        /// <summary>Awaitable task that completes when the transfer reaches a terminal state.</summary>
        public Task Completion => _tcs.Task;

        /// <summary>
        /// Cancelled when no progress update has been received within the configured inactivity timeout.
        /// Reset on every call to <see cref="ReportProgress"/>.
        /// </summary>
        public CancellationToken InactivityToken => _inactivityCts.Token;

        public void ReportProgress(NearbyTransferProgress transferProgress)
        {
            // Reset the inactivity timer — transfer is still alive
            var old = Interlocked.Exchange(ref _inactivityCts, new CancellationTokenSource(inactivityTimeout));
            old.Dispose();

            progress?.Report(transferProgress);

            switch (transferProgress.Status)
            {
                case NearbyTransferStatus.Success:
                    _tcs.TrySetResult();
                    break;
                case NearbyTransferStatus.Failure:
                    _tcs.TrySetException(new InvalidOperationException($"Transfer {transferProgress.PayloadId} failed."));
                    break;
                case NearbyTransferStatus.Canceled:
                    _tcs.TrySetCanceled();
                    break;
            }
        }

        public void Dispose()
        {
            _inactivityCts.Dispose();
            payload.Dispose();
            stream.Dispose();
        }
    }

    static Payload BuildStreamNameBytePayload(long streamPayloadId, string streamName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(streamName);
        var metadata = new byte[12 + nameBytes.Length];

        BinaryPrimitives.WriteUInt32LittleEndian(metadata, StreamNameMagic);
        BinaryPrimitives.WriteInt64LittleEndian(metadata.AsSpan(4), streamPayloadId);
        nameBytes.CopyTo(metadata, 12);

        return Payload.FromBytes(metadata);
    }

    static NearbyTransferStatus ToNearbyTransferStatus(int androidStatus) => androidStatus switch
    {
        PayloadTransferUpdate.Status.InProgress => NearbyTransferStatus.InProgress,
        PayloadTransferUpdate.Status.Success => NearbyTransferStatus.Success,
        PayloadTransferUpdate.Status.Failure => NearbyTransferStatus.Failure,
        PayloadTransferUpdate.Status.Canceled => NearbyTransferStatus.Canceled,
        _ => NearbyTransferStatus.InProgress
    };

    sealed class ConnectionRequestCallback(
        Action<string, ConnectionInfo> onConnectionInitiated,
        Action<string, ConnectionResolution> onConnectionResult,
        Action<string> onDisconnected) : ConnectionLifecycleCallback
    {
        public override void OnConnectionInitiated(string p0, ConnectionInfo p1)
            => onConnectionInitiated(p0, p1);

        public override void OnConnectionResult(string p0, ConnectionResolution p1)
            => onConnectionResult(p0, p1);

        public override void OnDisconnected(string p0)
            => onDisconnected(p0);
    }

    sealed class ConnectionCallback(
        Action<string, Payload> onPayloadReceived,
        Func<string, PayloadTransferUpdate, Task> onPayloadTransferUpdate) : PayloadCallback
    {
        public override void OnPayloadReceived(string p0, Payload p1)
            => onPayloadReceived(p0, p1);

        public override async void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            => await onPayloadTransferUpdate(p0, p1);
    }
}