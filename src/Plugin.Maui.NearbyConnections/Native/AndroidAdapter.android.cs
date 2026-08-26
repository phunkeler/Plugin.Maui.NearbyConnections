using Android.Content;
using AndroidUri = Android.Net.Uri;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The Android backend: maps <see cref="IPlatformAdapter"/> onto Google Nearby Connections.
/// Outbound operations go through the interface; inbound GMS callbacks call the bridge's internal
/// methods directly — the surface the device tests drive.
/// </summary>
sealed partial class AndroidAdapter : IPlatformAdapter
{
    readonly PlatformBridge _bridge;

    /// <param name="bridge">The shared platform layer this adapter feeds.</param>
    public AndroidAdapter(PlatformBridge bridge) => _bridge = bridge;

    readonly ConcurrentDictionary<long, (string DeviceId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, OutgoingTransfer> _outgoingTransfers = [];

    // Story S8 bookkeeping. The name frame and the stream payload race — GMS completes the bytes
    // frame through the work queue while the stream payload arrives directly — so whichever half
    // lands first waits for the other, keyed by the platform payload id.
    //
    // One lock guards both maps, and it is not an optimisation to split it. Each half must test for
    // its partner and park itself as one atomic step: with independent concurrent maps, both halves
    // can miss each other and park, and the payload is then never delivered.
    readonly Lock _streamGate = new();
    readonly Dictionary<long, string> _pendingStreamNames = [];
    readonly Dictionary<long, (string DeviceId, Stream Stream)> _parkedStreams = [];

    IConnectionsClient? _advertiseClient;
    IConnectionsClient? _discoverClient;

    public async Task StartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _advertiseClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            await _advertiseClient.StartAdvertisingAsync(
                _bridge.Options.DisplayName,
                _bridge.Options.ServiceId,
                new AdvertiseCallback(
                    OnConnectionInitiatedAsync,
                    OnConnectionResult,
                    OnDisconnected,
                    (endpointId, ex) => _bridge.LogCallbackError(nameof(ConnectionLifecycleCallback.OnConnectionInitiated), _bridge.PeerLookup.DeviceIdFor(endpointId), ex)),
                new AdvertisingOptions.Builder()
                    .SetStrategy(_bridge.Options.ToPlatformStrategy())
                    .SetLowPower(_bridge.Options.Android.UseLowPower)
                    .SetConnectionType(_bridge.Options.ToPlatformConnectionType())
                    .Build()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _bridge.LogStartAdvertisingFailed(ex);
            throw new NearbyAdvertisingException("Failed to start advertising.", ex);
        }
    }

    public void StopAdvertising()
    {
        _advertiseClient?.StopAdvertising();
        _advertiseClient?.Dispose();
        _advertiseClient = null;
    }

    internal async Task OnConnectionInitiatedAsync(string endpointId, ConnectionInfo connectionInfo)
    {
        try
        {
            var deviceId = _bridge.PeerLookup.DeviceIdFor(endpointId);
            var device = _bridge.PeerLookup.Record(deviceId, connectionInfo.EndpointName);

            if (connectionInfo.IsIncomingConnection)
            {
                _bridge.LogConnectionRequestReceived(device.Id, device.DisplayName);

                var tcs = _bridge.RegisterConnectionTcs(deviceId, CancellationToken.None);
                var request = new NearbyConnectionRequest(
                    device,
                    accept: ct =>
                    {
                        _bridge.AttachConnectionTcsToken(deviceId, ct);

                        return _bridge.AwaitHandshakeAsync(
                            device,
                            tcs,
                            ConnectionRole.Acceptor,
                            beforeAwait: _ => RespondToConnectionAsync(device, accept: true),
                            ct);
                    },
                    reject: ct =>
                    {
                        _bridge._connectionTcs.TryRemove(deviceId, out _);
                        return RespondToConnectionAsync(device, accept: false);
                    });

                _bridge.WriteConnectionRequest(request);
            }
            else
            {
                await RespondToConnectionAsync(device, accept: true).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            var deviceId = _bridge.PeerLookup.DeviceIdFor(endpointId);

            _bridge.LogCallbackError(nameof(OnConnectionInitiatedAsync), deviceId, ex);
            _bridge.FaultConnectionTcs(deviceId, ex);
        }
    }

    internal void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        try
        {
            var deviceId = _bridge.PeerLookup.DeviceIdFor(endpointId);

            _bridge.LogConnectionResult(
                deviceId,
                resolution.Status.StatusCode,
                resolution.Status.StatusMessage ?? string.Empty,
                resolution.Status.IsSuccess);

            if (resolution.Status.IsSuccess)
            {
                if (!_bridge.PeerLookup.TryGetDevice(deviceId, out var device))
                {
                    _bridge.FaultConnectionTcs(deviceId, new NearbyException($"Device not found in manager for device '{deviceId}' after successful connection."));
                    return;
                }

                _bridge.CompleteHandshake(device, new AndroidConnection(this, deviceId));
            }
            else
            {
                if (_bridge.PeerLookup.Remove(deviceId) is { } lostDevice)
                {
                    _bridge.WriteDeviceLost(lostDevice);
                }

                _bridge.FaultConnectionTcs(deviceId, new NearbyException(
                    $"Connection to device '{deviceId}' failed: {resolution.Status.StatusMessage} (code {resolution.Status.StatusCode})."));
            }
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnConnectionResult), _bridge.PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    internal void OnDisconnected(string endpointId)
    {
        try
        {
            var deviceId = _bridge.PeerLookup.DeviceIdFor(endpointId);

            _bridge.LogDeviceDisconnected(deviceId);

            // A GMS callback: the signature is fixed, so the release is tracked rather than awaited.
            _bridge.ReleaseConnectionFromCallback(deviceId);
            _bridge.PeerLookup.Remove(deviceId);
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnDisconnected), _bridge.PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    #region Discovery

    public async Task StartDiscoveryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _discoverClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            await _discoverClient.StartDiscoveryAsync(
                _bridge.Options.ServiceId,
                new DiscoveryCallback(OnEndpointFound, OnEndpointLost),
                new DiscoveryOptions.Builder()
                    .SetStrategy(_bridge.Options.ToPlatformStrategy())
                    .SetLowPower(_bridge.Options.Android.UseLowPower)
                    .Build()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _bridge.LogStartDiscoveringFailed(ex);

            throw new NearbyDiscoveryException("Failed to start discovery.", ex);
        }
    }

    public void StopDiscovering()
    {
        _discoverClient?.StopDiscovery();
        _discoverClient?.Dispose();
        _discoverClient = null;
    }

    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        try
        {
            var device = _bridge.PeerLookup.Record(_bridge.PeerLookup.DeviceIdFor(endpointId), info.EndpointName);
            _bridge.OnDeviceFound(device);
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnEndpointFound), _bridge.PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    internal void OnEndpointLost(string endpointId)
    {
        try
        {
            _bridge.OnDeviceLost(_bridge.PeerLookup.DeviceIdFor(endpointId));
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnEndpointLost), _bridge.PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    #endregion Discovery

    internal void OnPayloadReceived(string endpointId, Payload payload)
    {
        try
        {
            var deviceId = _bridge.PeerLookup.DeviceIdFor(endpointId);

            _bridge.LogPayloadReceived(deviceId, payload.Id, payload.PayloadType);

            if (payload.PayloadType == Payload.Type.Stream)
            {
                // Streams deliver on receipt — live data must not wait for a terminal update.
                HandleInboundStream(deviceId, payload);
                return;
            }

            _incomingPayloads.TryAdd(payload.Id, (deviceId, payload));
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnPayloadReceived), _bridge.PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    internal async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        try
        {
            var deviceId = _bridge.PeerLookup.DeviceIdFor(endpointId);

            _bridge.LogPayloadTransferUpdate(deviceId, update.PayloadId, update.TransferStatus, update.TotalBytes, update.BytesTransferred);

            if (_outgoingTransfers.TryGetValue(update.PayloadId, out var outgoingTransfer))
            {
                var status = ToNearbyTransferStatus(update.TransferStatus);
                outgoingTransfer.OnUpdate(new NearbyTransferProgress(
                    payloadId: update.PayloadId,
                    bytesTransferred: update.BytesTransferred,
                    totalBytes: update.TotalBytes,
                    status));

                return;
            }

            if (update.TransferStatus == PayloadTransferUpdate.Status.InProgress
                && _incomingPayloads.TryGetValue(update.PayloadId, out var inboundEntry)
                && _bridge._activeConnections.TryGetValue(inboundEntry.DeviceId, out var inboundPair))
            {
                inboundPair.Connection.InboundProgress?.Report(new NearbyTransferProgress(
                    payloadId: update.PayloadId,
                    bytesTransferred: update.BytesTransferred,
                    totalBytes: update.TotalBytes,
                    NearbyTransferStatus.InProgress));
            }

            if (update.TransferStatus == PayloadTransferUpdate.Status.Success)
            {
                var payloadId = update.PayloadId;

                await _bridge.WorkQueue
                    .Enqueue(deviceId, () => OnIncomingPayloadSuccess(deviceId, payloadId))
                    .ConfigureAwait(false);
            }
            else if (update.TransferStatus is PayloadTransferUpdate.Status.Failure or PayloadTransferUpdate.Status.Canceled
                && _incomingPayloads.TryRemove(update.PayloadId, out var deadEntry))
            {
                _bridge.LogIncomingPayloadProcessingFailed(deviceId, update.PayloadId);
                DisposeIncomingPayload(deadEntry.Payload);
            }
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnPayloadTransferUpdate), _bridge.PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    async Task OnIncomingPayloadSuccess(string deviceId, long payloadId)
    {
        if (!_incomingPayloads.TryRemove(payloadId, out var entry))
        {
            return;
        }

        // The copy is bounded by the connection's own teardown: DisconnectedToken is cancelled by
        // CompleteReceive, and DisposeAsync disposes every active connection, so disposing the
        // session cancels an in-flight copy too. Without a live connection the copied file has
        // nowhere to go, so skip the work rather than copy a file WritePayload will drop.
        var copyToken = _bridge._activeConnections.TryGetValue(deviceId, out var pair)
            ? pair.Connection.DisconnectedToken
            : new CancellationToken(canceled: true);

        if (entry.Payload.PayloadType != Payload.Type.File
            && entry.Payload.AsBytes() is { } maybeControl
            && ControlMessage.TryDecode(maybeControl, out var controlType))
        {
            HandleControlFrame(deviceId, controlType, maybeControl);
            DisposeIncomingPayload(entry.Payload);
            return;
        }

        NearbyPayload? nearbyPayload = entry.Payload.PayloadType == Payload.Type.File
            ? await CopyFilePayloadAsync(entry.Payload, StagingDirectory, copyToken).ConfigureAwait(false)
            : entry.Payload.AsBytes() is { } bytes
                ? new NearbyBytesPayload(bytes)
                : null;

        if (nearbyPayload is not null)
        {
            _bridge.WritePayload(deviceId, nearbyPayload);
        }
        else if (!copyToken.IsCancellationRequested)
        {
            // A cancelled copy already logged its own teardown message. Reporting it again as a
            // processing failure would raise a routine disconnect to Error.
            _bridge.LogIncomingPayloadProcessingFailed(deviceId, payloadId);
        }

        DisposeIncomingPayload(entry.Payload);
    }

    static void DisposeIncomingPayload(Payload payload)
    {
        payload.Close();
        payload.Dispose();
    }

    /// <summary>
    /// Opens a named outbound stream (story S8): sends the name frame first, then the stream
    /// payload, on the same ordered channel. The returned stream is the writable half of a
    /// managed pipe whose reader GMS consumes; disposing it ends the stream for the remote peer.
    /// </summary>
    internal async Task<Stream> OpenStreamAsync(string deviceId, string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_bridge._activeConnections.ContainsKey(deviceId) || !_bridge.PeerLookup.TryGetEndpointId(deviceId, out var endpointId))
        {
            throw new NearbyException(
                $"Cannot open a stream: no active connection for device '{deviceId}'.");
        }

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var pipe = Android.OS.ParcelFileDescriptor.CreatePipe()
            ?? throw new NearbyTransferException("Cannot open a stream: the platform could not create a pipe.");
        var payload = Payload.FromStream(pipe[0])
            ?? throw new NearbyTransferException("Cannot open a stream: the platform rejected the stream payload.");

        try
        {
            using (var frame = Payload.FromBytes(ControlMessage.EncodeStreamName(payload.Id, name))!)
            {
                await client.SendPayloadAsync(endpointId, frame).ConfigureAwait(false);
            }

            await client.SendPayloadAsync(endpointId, payload).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not NearbyException)
        {
            _bridge.LogWriteError(nameof(OpenStreamAsync), deviceId, ex);
            throw new NearbyTransferException(
                $"Failed to open a stream to device '{deviceId}'.", ex);
        }

        // Wrapped for the same reason the inbound half is: a pipe is not seekable, and the raw
        // invoker claims otherwise.
        return new NonSeekableStream(
            new Android.Runtime.OutputStreamInvoker(new Android.OS.ParcelFileDescriptor.AutoCloseOutputStream(pipe[1])));
    }

    void HandleInboundStream(string deviceId, Payload payload)
    {
        var raw = payload.AsStream()?.AsInputStream();

        if (raw is null)
        {
            _bridge.LogIncomingPayloadProcessingFailed(deviceId, payload.Id);
            return;
        }

        // Wrapped, not raw: the GMS stream is a pipe that claims to be seekable and then throws on
        // Position, which is the first thing CopyToAsync reads. See NonSeekableStream.
        var stream = new NonSeekableStream(raw);

        string? name;

        lock (_streamGate)
        {
            if (!_pendingStreamNames.Remove(payload.Id, out name))
            {
                _parkedStreams[payload.Id] = (deviceId, stream);
            }
        }

        if (name is not null)
        {
            _bridge.WritePayload(deviceId, new NearbyStreamPayload(stream, name));
        }
    }

    void HandleControlFrame(string deviceId, ControlMessageType type, byte[] frame)
    {
        switch (type)
        {
            case ControlMessageType.StreamName
                when ControlMessage.TryDecodeStreamName(frame, out var payloadId, out var name):
                bool paired;
                (string DeviceId, Stream Stream) parked;

                lock (_streamGate)
                {
                    paired = _parkedStreams.Remove(payloadId, out parked);

                    if (!paired)
                    {
                        _pendingStreamNames[payloadId] = name!;
                    }
                }

                if (paired)
                {
                    _bridge.WritePayload(parked.DeviceId, new NearbyStreamPayload(parked.Stream, name!));
                }

                break;

            case ControlMessageType.Disconnect:
                // Same handling as the iOS sibling: release, tracked rather than awaited.
                _bridge.ReleaseConnectionFromCallback(deviceId);
                break;

            default:
                _bridge.LogUnknownControlMessageType(type);
                break;
        }
    }

    async Task<NearbyFilePayload?> CopyFilePayloadAsync(
        Payload payload,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        // Both wrappers are disposed, and both are declared at method scope on purpose: the finally
        // below still reads sourceUri, and a narrower using block would dispose it first and throw
        // ArgumentException ("'jobject' must not be IntPtr.Zero"). Disposing a managed callable
        // wrapper releases only its JNI global reference, never the Java object — the Payload owns
        // that and is closed separately in DisposeIncomingPayload. Left undisposed, each received
        // file payload holds two global references until finalization.
        using var sourceFile = payload.AsFile();
        using var sourceUri = sourceFile?.AsUri();

        if (sourceUri is null)
        {
            return null;
        }

        var fileName = ResolveResourceName(sourceUri);
        var source = sourceUri.ToString()!;
        string? destinationPath = null;

        try
        {
            using var inputStream = Application.Context.ContentResolver?.OpenInputStream(sourceUri);

            if (inputStream is null)
            {
                return null;
            }

            var outputStream = PlatformBridge.ClaimUniqueDestinationPath(destinationDirectory, fileName);
            destinationPath = outputStream.Name;

            using (outputStream)
            {
                await inputStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Teardown, not a failure: the connection dropped or the session was disposed mid-copy.
            // Logged at Debug rather than Error so a routine disconnect does not report a fault.
            _bridge.LogFileCopyCanceled(source, destinationPath ?? destinationDirectory);
            _bridge.DeletePartialDestination(destinationPath);
            return null;
        }
        catch (Exception ex)
        {
            _bridge.LogFileCopyFailed(source, destinationPath ?? destinationDirectory, ex);
            _bridge.DeletePartialDestination(destinationPath);
            return null;
        }
        finally
        {
            // Deleted on every path, cancellation included. A cancelled payload is undeliverable
            // anyway — the connection is gone — so keeping the GMS original would leave a file in
            // shared storage that nothing will ever collect.
            try
            {
                Application.Context.ContentResolver?.Delete(sourceUri, null, null);
            }
            catch (Exception ex)
            {
                _bridge.LogFileDeleteFailed(sourceUri.ToString()!, ex);
            }
        }

        return new NearbyFilePayload(new FileResult(destinationPath));
    }

    public async Task InitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_bridge.PeerLookup.TryGetEndpointId(device.Id, out var endpointId))
        {
            _bridge.FaultConnectionTcs(device.Id, new NearbyException(
                $"Cannot connect: device '{device.DisplayName}' (Id={device.Id}) is not currently visible. Ensure it is actively advertising and within range."));
            return;
        }

        try
        {
            // Must be awaited HERE, not returned directly
            await NearbyClass
                .GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
                .RequestConnectionAsync(
                    _bridge.Options.DisplayName,
                    endpointId,
                    new AdvertiseCallback(
                        OnConnectionInitiatedAsync,
                        OnConnectionResult,
                        OnDisconnected,
                        (endpointId, ex) => _bridge.LogCallbackError(nameof(ConnectionLifecycleCallback.OnConnectionInitiated), _bridge.PeerLookup.DeviceIdFor(endpointId), ex))).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            try
            {
                NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
                    .DisconnectFromEndpoint(endpointId);
            }
            catch (Exception disconnectEx)
            {
                _bridge.LogFailedToClearStaleConnectionState(device.Id, disconnectEx);
            }

            _bridge.FaultConnectionTcs(device.Id, new NearbyException(
                $"Failed to initiate connection to device '{device.Id}'.", ex));
        }
    }

    internal Task RespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        if (!_bridge.PeerLookup.TryGetEndpointId(device.Id, out var endpointId))
        {
            throw new NearbyException(
                $"Cannot respond to the connection request from device '{device.Id}': the platform no longer tracks it.");
        }

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return accept
            ? client.AcceptConnectionAsync(endpointId, new ConnectionCallback(
                OnPayloadReceived,
                OnPayloadTransferUpdate,
                (callbackEndpointId, ex) => _bridge.LogCallbackError(nameof(PayloadCallback.OnPayloadTransferUpdate), _bridge.PeerLookup.DeviceIdFor(callbackEndpointId), ex)))
            : client.RejectConnectionAsync(endpointId);
    }

    public async Task AbandonConnectAsync(NearbyDevice device)
    {
        try
        {
            await DisconnectEndpointAsync(device.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _bridge.LogAbandonConnectError(device.Id, ex);
        }
    }

    internal async ValueTask DisconnectEndpointAsync(string deviceId)
    {
        _bridge.LogDisconnecting(deviceId, _bridge.PeerLookup.TryGetDevice(deviceId, out var d)
            ? d.DisplayName
            : null);

        if (_bridge.PeerLookup.TryGetEndpointId(deviceId, out var endpointId))
        {
            var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
            client.DisconnectFromEndpoint(endpointId);
        }

        await _bridge.ReleaseConnectionAsync(deviceId).ConfigureAwait(false);
        _bridge.PeerLookup.Remove(deviceId);
    }

    internal async Task SendBytesAsync(
        string deviceId,
        byte[] data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_bridge._activeConnections.ContainsKey(deviceId) || !_bridge.PeerLookup.TryGetEndpointId(deviceId, out var endpointId))
        {
            throw new NearbyException(
                $"Cannot send bytes: no active connection for device '{deviceId}'.");
        }

        using var payload = Payload.FromBytes(data);
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            await client.SendPayloadAsync(endpointId, payload).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _bridge.LogSendBytesFailed(deviceId, ex);
            throw new NearbyTransferException(
                $"Failed to send bytes to device '{deviceId}'.", ex);
        }
    }

    internal async Task SendFileAsync(
        string deviceId,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_bridge._activeConnections.ContainsKey(deviceId) || !_bridge.PeerLookup.TryGetEndpointId(deviceId, out var endpointId))
        {
            throw new NearbyException(
                $"Cannot send file: no active connection for device '{deviceId}'.");
        }

        using var androidUri = TryCreateUri(uri);

        if (androidUri is null)
        {
            _bridge.LogInvalidFileUri(uri);
            throw new NearbyTransferException("Cannot send file: the URI is not a valid or supported scheme. Use a file:// or content:// URI.");
        }

        var filePayload = BuildFilePayload(androidUri) ?? throw new NearbyTransferException("Cannot send file: failed to open the file descriptor for the given URI.");
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var transfer = new OutgoingTransfer(progress, _bridge.Options.TransferInactivityTimeout, _bridge.TimeProvider);

        _outgoingTransfers.TryAdd(filePayload.Id, transfer);

        async Task CancelPayloadLoggedAsync()
        {
            try
            {
                await client.CancelPayloadAsync(filePayload.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _bridge.LogWriteError(nameof(SendFileAsync), deviceId, ex);
            }
        }

        // Every terminal path reports the bytes transferred so far against the same total; only the
        // status differs. Mirrors the iOS Report helper — reporting 0/0 here would snap a bound
        // progress bar to zero on cancel instead of leaving it where the transfer actually stopped.
        void Report(NearbyTransferStatus status)
        {
            var (BytesTransferred, TotalBytes) = transfer.LastProgress;

            progress?.Report(new NearbyTransferProgress(
                payloadId: filePayload.Id,
                bytesTransferred: BytesTransferred,
                totalBytes: TotalBytes,
                status));
        }

        // The send and the completion await run as one task, so a failed hand-off to GMS flows
        // through the same shared catch ladder as a failed transfer.
        async Task SendThenAwaitCompletionAsync()
        {
            await client.SendPayloadAsync(endpointId, filePayload).ConfigureAwait(false);
            await transfer.Completion.ConfigureAwait(false);
        }

        try
        {
            await _bridge.AwaitFileTransferAsync(
                deviceId,
                transfer,
                SendThenAwaitCompletionAsync(),
                Report,
                cancelPlatformTransfer: () => _ = CancelPayloadLoggedAsync(),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _outgoingTransfers.TryRemove(filePayload.Id, out _);
            transfer.Dispose();
            filePayload.Close();
            filePayload.Dispose();
        }
    }

    Payload? BuildFilePayload(AndroidUri uri)
    {
        try
        {
            var parcelFileDescriptor = Application.Context.ContentResolver?.OpenFileDescriptor(uri, "r");
            var payload = parcelFileDescriptor is not null
                ? Payload.FromFile(parcelFileDescriptor)
                : null;
            var fileName = ResolveResourceName(uri);

            payload?.SetFileName(fileName);
            payload?.SetSensitive(true);

            return payload;
        }
        catch (Exception ex)
        {
            _bridge.LogBuildFilePayloadFailed(ex);
        }

        return null;
    }

    public async Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        var result = NearbyAvailability.Ready;
        var context = Platform.CurrentActivity ?? Platform.AppContext;

        cancellationToken.ThrowIfCancellationRequested();

        if (GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(context) != ConnectionResult.Success)
        {
            result |= NearbyAvailability.PlayServicesUnavailable;
        }

        if (!await ArePermissionsGrantedAsync().ConfigureAwait(false))
        {
            result |= NearbyAvailability.MissingPermissions;
        }

        try
        {
            using var bluetoothManager = (Android.Bluetooth.BluetoothManager?)context.GetSystemService(Context.BluetoothService);

            if (bluetoothManager?.Adapter is { } adapter && !adapter.IsEnabled)
            {
                result |= NearbyAvailability.BluetoothDisabled;
            }
        }
        catch (Exception ex)
        {
            _bridge.LogAvailabilityCheckPartiallyFailed(nameof(NearbyAvailability.BluetoothDisabled), ex);
        }

        try
        {
            using var wifiManager = (Android.Net.Wifi.WifiManager?)context.GetSystemService(Context.WifiService);

            if (wifiManager is not null && !wifiManager.IsWifiEnabled)
            {
                result |= NearbyAvailability.WifiDisabled;
            }
        }
        catch (Exception ex)
        {
            _bridge.LogAvailabilityCheckPartiallyFailed(nameof(NearbyAvailability.WifiDisabled), ex);
        }

        return result;
    }

    static async Task<bool> ArePermissionsGrantedAsync()
    {
        if (await Permissions.CheckStatusAsync<Permissions.Bluetooth>().ConfigureAwait(false) != PermissionStatus.Granted)
        {
            return false;
        }

        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false) == PermissionStatus.Granted;
        }

        return !OperatingSystem.IsAndroidVersionAtLeast(33)
            || await Permissions.CheckStatusAsync<Permissions.NearbyWifiDevices>().ConfigureAwait(false) == PermissionStatus.Granted;
    }

    public void ReleaseConnection(string deviceId)
    {
        // Runs after PlatformQuiesceConnectionAsync, so no copy is still reading these.
        foreach (var (payloadId, entry) in _incomingPayloads)
        {
            if (entry.DeviceId == deviceId
                && _incomingPayloads.TryRemove(payloadId, out var removed))
            {
                DisposeIncomingPayload(removed.Payload);
            }
        }
    }

    string? _stagingDirectory;

    /// <inheritdoc/>
    /// <remarks>
    /// Per instance: each adapter stages into its own subdirectory of the shared staging root, so
    /// two platform instances in one process cannot collide (re-assessment fix 6 — the last
    /// process-wide mutable fact). Disposal sweeps the whole root, orphans included.
    /// </remarks>
    public string StagingDirectory => _stagingDirectory ??= Path.Combine(
        FileSystem.CacheDirectory,
        PlatformBridge.StagingDirectoryName,
        Guid.NewGuid().ToString("N"));

    public void SweepStaging() => _bridge.SweepStagingDirectory(Path.Combine(FileSystem.CacheDirectory, PlatformBridge.StagingDirectoryName));

    public void Dispose()
    {
        foreach (var (_, entry) in _incomingPayloads)
        {
            DisposeIncomingPayload(entry.Payload);
        }
        _incomingPayloads.Clear();

        // Both maps are plain dictionaries under _streamGate, so a racing callback would otherwise
        // fault this enumeration.
        lock (_streamGate)
        {
            foreach (var (_, parked) in _parkedStreams)
            {
                parked.Stream.Dispose();
            }
            _parkedStreams.Clear();
            _pendingStreamNames.Clear();
        }

        foreach (var (_, transfer) in _outgoingTransfers)
        {
            transfer.Dispose();
        }
        _outgoingTransfers.Clear();
    }

    static NearbyTransferStatus ToNearbyTransferStatus(int androidStatus) => androidStatus switch
    {
        PayloadTransferUpdate.Status.InProgress => NearbyTransferStatus.InProgress,
        PayloadTransferUpdate.Status.Success => NearbyTransferStatus.Success,
        PayloadTransferUpdate.Status.Failure => NearbyTransferStatus.Failure,
        PayloadTransferUpdate.Status.Canceled => NearbyTransferStatus.Canceled,
        _ => NearbyTransferStatus.InProgress
    };

    sealed class AdvertiseCallback(
        Func<string, ConnectionInfo, Task> onConnectionInitiated,
        Action<string, ConnectionResolution> onConnectionResult,
        Action<string> onDisconnected,
        Action<string, Exception>? onError = null) : ConnectionLifecycleCallback
    {
        public override async void OnConnectionInitiated(string p0, ConnectionInfo p1)
        {
            try
            {
                await onConnectionInitiated(p0, p1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onError?.Invoke(p0, ex);
            }
        }

        public override void OnConnectionResult(string p0, ConnectionResolution p1)
            => onConnectionResult(p0, p1);

        public override void OnDisconnected(string p0)
            => onDisconnected(p0);
    }

    sealed class DiscoveryCallback(
        Action<string, DiscoveredEndpointInfo> onEndpointFound,
        Action<string> onEndpointLost) : EndpointDiscoveryCallback
    {
        public override void OnEndpointFound(string p0, DiscoveredEndpointInfo p1)
            => onEndpointFound(p0, p1);

        public override void OnEndpointLost(string p0)
            => onEndpointLost(p0);
    }

    sealed class ConnectionCallback(
        Action<string, Payload> onPayloadReceived,
        Func<string, PayloadTransferUpdate, Task> onPayloadTransferUpdate,
        Action<string, Exception>? onError = null) : PayloadCallback
    {
        public override void OnPayloadReceived(string p0, Payload p1)
            => onPayloadReceived(p0, p1);

        public override async void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
        {
            try
            {
                await onPayloadTransferUpdate(p0, p1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onError?.Invoke(p0, ex);
            }
        }
    }
}