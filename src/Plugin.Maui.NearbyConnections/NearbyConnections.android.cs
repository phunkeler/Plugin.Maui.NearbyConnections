namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    readonly ConcurrentDictionary<long, IProgress<NearbyTransferProgress>> _outgoingProgress = new();
    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = new();

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

        // Buffer the payload — we wait for OnPayloadTransferUpdate Success before raising DataReceived.
        // BYTES payloads are complete immediately; FILE/STREAM require the success update.
        _incomingPayloads[payload.Id] = (endpointId, payload);
    }

    async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        Trace.TraceInformation($"Payload transfer update: EndpointId={endpointId}, PayloadId={update.PayloadId}, TransferStatus={update.TransferStatus}, TotalBytes={update.TotalBytes}, BytesTransferred={update.BytesTransferred}");

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

        if (!_deviceManager.TryGetDevice(endpointId, out var device))
        {
            return;
        }


        // Raise incoming transfer progress

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


        // On success, raise DataReceived
        if (update.TransferStatus == PayloadTransferUpdate.Status.Success
            && _incomingPayloads.TryRemove(update.PayloadId, out var entry))
        {
            NearbyPayload? nearbyPayload = entry.Payload.PayloadType switch
            {
                Payload.Type.Bytes => entry.Payload.AsBytes() is { } bytes
                    ? new BytesPayload(bytes)
                    : null,
                Payload.Type.Stream => BuildStreamPayload(entry.Payload.AsStream()),
                _ => null
            };

            if (nearbyPayload is not null)
            {
                Events.OnDataReceived(device, nearbyPayload, TimeProvider.GetUtcNow());
            }

            entry.Payload.Dispose();
        }
    }

    static StreamPayload? BuildStreamPayload(Payload.Stream? payloadStream)
    {
        if (payloadStream is null)
        {
            return null;
        }

        var inputStream = payloadStream.AsInputStream();

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

    async Task PlatformSendAsync(
        NearbyDevice device,
        byte[] data,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var androidPayload = Payload.FromBytes(data);

        if (progress is not null)
        {
            _outgoingProgress[androidPayload.Id] = progress;
        }

        try
        {
            await client.SendPayloadAsync(device.Id, androidPayload);
        }
        catch
        {
            _outgoingProgress.TryRemove(androidPayload.Id, out _);
            throw;
        }
    }

    async Task PlatformSendAsync(
        NearbyDevice device,
        Func<Task<Stream>> streamFactory,
        string streamName,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var stream = await streamFactory();
        var payload = Payload.FromStream(stream);

        if (progress is not null)
        {
            _outgoingProgress[payload.Id] = progress;
        }

        try
        {
            await client.SendPayloadAsync(device.Id, payload);
        }
        catch
        {
            _outgoingProgress.TryRemove(payload.Id, out _);
            throw;
        }
    }

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