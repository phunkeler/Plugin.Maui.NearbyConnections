namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    readonly ConcurrentDictionary<long, IProgress<NearbyTransferProgress>> _outgoingProgress = new();
    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = new();

    #region Discovery

    public void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        Trace.WriteLine($"Endpoint found: EndpointId={endpointId}, EndpointName={info.EndpointName}");
        var device = _deviceManager.DeviceFound(endpointId, info.EndpointName);
        Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    public void OnEndpointLost(string endpointId)
    {
        Trace.WriteLine($"Endpoint lost: EndpointId={endpointId}");

        if (_deviceManager.TryGetDevice(endpointId, out var existingDevice)
            && existingDevice?.State == NearbyDeviceState.Connected)
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
        Trace.WriteLine($"Connection initiated: EndpointId={endpointId}, EndpointName={connectionInfo.EndpointName}, IsIncomingConnection={connectionInfo.IsIncomingConnection}");

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
            // Skip this extra step. The discoverer already initiated the connection request.
            // TODO: Consider exposing this extra step as a "security" option and optionally leverages the 4 Digit Auth Token in ConnectionInfo.
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
        Trace.WriteLine($"Connection result: EndpointId={endpointId}, StatusCode={resolution.Status.StatusCode}, StatusMessage={resolution.Status.StatusMessage ?? string.Empty}, IsSuccess={resolution.Status.IsSuccess}");

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
        Trace.WriteLine($"Disconnected from EndpointId={endpointId}");

        var device = _deviceManager.DeviceDisconnected(endpointId);

        if (device is not null)
        {
            Events.OnDeviceDisconnected(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Advertising

    void OnPayloadReceived(string endpointId, Payload payload)
    {
        Trace.WriteLine($"Payload received: EndpointId={endpointId}, PayloadId={payload.Id}, PayloadType={payload.PayloadType}");

        // Buffer the payload — we wait for OnPayloadTransferUpdate Success before raising DataReceived.
        // BYTES payloads are complete immediately; FILE/STREAM require the success update.
        _incomingPayloads[payload.Id] = (endpointId, payload);
    }

    void OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        Trace.WriteLine($"Payload transfer update: EndpointId={endpointId}, PayloadId={update.PayloadId}, TransferStatus={update.TransferStatus}, TotalBytes={update.TotalBytes}, BytesTransferred={update.BytesTransferred}");

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == endpointId);

        // Route outgoing progress
        if (_outgoingProgress.TryGetValue(update.PayloadId, out var outProgress))
        {
            var status = update.TransferStatus switch
            {
                PayloadTransferUpdate.Status.InProgress => NearbyTransferStatus.InProgress,
                PayloadTransferUpdate.Status.Success => NearbyTransferStatus.Success,
                PayloadTransferUpdate.Status.Failure => NearbyTransferStatus.Failure,
                PayloadTransferUpdate.Status.Canceled => NearbyTransferStatus.Canceled,
                _ => NearbyTransferStatus.InProgress
            };

            outProgress.Report(new NearbyTransferProgress(
                payloadId: update.PayloadId,
                bytesTransferred: update.BytesTransferred,
                totalBytes: update.TotalBytes,
                status));

            if (status != NearbyTransferStatus.InProgress)
            {
                _outgoingProgress.TryRemove(update.PayloadId, out _);
            }
        }

        // Raise incoming transfer progress
        if (device is not null)
        {
            var incomingStatus = update.TransferStatus switch
            {
                PayloadTransferUpdate.Status.InProgress => NearbyTransferStatus.InProgress,
                PayloadTransferUpdate.Status.Success => NearbyTransferStatus.Success,
                PayloadTransferUpdate.Status.Failure => NearbyTransferStatus.Failure,
                PayloadTransferUpdate.Status.Canceled => NearbyTransferStatus.Canceled,
                _ => NearbyTransferStatus.InProgress
            };

            Events.OnIncomingTransferProgress(
                device,
                new NearbyTransferProgress(update.PayloadId, update.BytesTransferred, update.TotalBytes, incomingStatus),
                TimeProvider.GetUtcNow());
        }

        // On success, raise DataReceived
        if (update.TransferStatus == PayloadTransferUpdate.Status.Success
            && _incomingPayloads.TryRemove(update.PayloadId, out var entry)
            && device is not null)
        {
            NearbyPayload? nearbyPayload = entry.Payload.PayloadType switch
            {
                Payload.Type.Bytes => entry.Payload.AsBytes() is { } bytes
                    ? new BytesPayload(bytes)
                    : null,
                Payload.Type.File => BuildFilePayload(entry.Payload.AsFile()),
                Payload.Type.Stream => BuildStreamPayload(entry.Payload.AsStream()),
                _ => null
            };

            if (nearbyPayload is not null)
            {
                Events.OnDataReceived(device, nearbyPayload, TimeProvider.GetUtcNow());
            }
        }
    }

    static FilePayload? BuildFilePayload(Payload.File? file)
    {
        if (file is null)
        {
            return null;
        }

        // AsParcelFileDescriptor gives access to the underlying file descriptor.
        // Map it to a path via /proc/self/fd/<fd> which is a symlink to the actual file.
        var pfd = file.AsParcelFileDescriptor();
        if (pfd is null)
        {
            return null;
        }

        var fdPath = $"/proc/self/fd/{pfd.Fd}";
        var resolved = new Java.IO.File(fdPath).CanonicalPath ?? fdPath;
        return new FilePayload(resolved);
    }

    static StreamPayload? BuildStreamPayload(Payload.Stream? payloadStream)
    {
        if (payloadStream is null)
        {
            return null;
        }

        var javaStream = payloadStream.AsInputStream();
        if (javaStream is null)
        {
            return null;
        }

        // Drain the Java InputStream into a MemoryStream synchronously (we are already on a
        // background callback thread) so the caller gets a replayable Stream factory.
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        int read;
        while ((read = javaStream.Read(buffer)) > 0)
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

    async Task PlatformSendAsync(NearbyDevice device, NearbyPayload payload, IProgress<NearbyTransferProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        Payload androidPayload;
        Task? pumpTask = null;

        switch (payload)
        {
            case BytesPayload bytes:
                androidPayload = Payload.FromBytes(bytes.Data);
                break;

            case FilePayload file:
                androidPayload = Payload.FromFile(new Java.IO.File(file.FilePath));
                break;

            case StreamPayload stream:
                // ParcelFileDescriptor.CreatePipe() returns [readEnd, writeEnd].
                // Nearby Connections reads from the read end; we pump the .NET stream into the write end.
                var pipe = Android.OS.ParcelFileDescriptor.CreatePipe()
                    ?? throw new InvalidOperationException("Failed to create pipe for stream payload.");
                androidPayload = Payload.FromStream(pipe[0]);
                pumpTask = Task.Run(async () =>
                {
                    try
                    {
                        using var writeStream = new Android.OS.ParcelFileDescriptor.AutoCloseOutputStream(pipe[1]);
                        using var source = stream.StreamFactory();
                        var buffer = new byte[16 * 1024];
                        int read;
                        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            writeStream.Write(buffer, 0, read);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                    }
                    finally
                    {
                        pipe[1].Close();
                    }
                }, cancellationToken);
                break;

            default:
                throw new ArgumentException($"Unsupported payload type: {payload.GetType().Name}", nameof(payload));
        }

        if (progress is not null)
        {
            _outgoingProgress[androidPayload.Id] = progress;
        }

        try
        {
            await client.SendPayloadAsync(device.Id, androidPayload);
            if (pumpTask is not null)
            {
                await pumpTask;
            }
        }
        catch
        {
            _outgoingProgress.TryRemove(androidPayload.Id, out _);
            throw;
        }
    }

    sealed class ConnectionRequestCallback(
        Action<string, ConnectionInfo> onConnectionInitiated,
        Action<string, ConnectionResolution> onConnectionResult,
        Action<string> onDisconnected) : ConnectionLifecycleCallback
    {
        public override void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
            => onConnectionInitiated(endpointId, connectionInfo);

        public override void OnConnectionResult(string endpointId, ConnectionResolution resolution)
            => onConnectionResult(endpointId, resolution);

        public override void OnDisconnected(string endpointId)
            => onDisconnected(endpointId);
    }

    sealed class ConnectionCallback(
        Action<string, Payload> onPayloadReceived,
        Action<string, PayloadTransferUpdate> onPayloadTransferUpdate) : PayloadCallback
    {
        public override void OnPayloadReceived(string p0, Payload p1)
            => onPayloadReceived(p0, p1);

        public override void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            => onPayloadTransferUpdate(p0, p1);
    }
}