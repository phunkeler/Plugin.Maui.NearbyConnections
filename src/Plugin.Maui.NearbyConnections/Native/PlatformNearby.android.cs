using Android.Content;
using AndroidUri = Android.Net.Uri;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    readonly ConcurrentDictionary<long, (string DeviceId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, OutgoingTransfer> _outgoingTransfers = [];

    IConnectionsClient? _advertiseClient;
    IConnectionsClient? _discoverClient;

    async Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _advertiseClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            await _advertiseClient.StartAdvertisingAsync(
                _options.DisplayName,
                _options.ServiceId,
                new AdvertiseCallback(
                    OnConnectionInitiatedAsync,
                    OnConnectionResult,
                    OnDisconnected,
                    (endpointId, ex) => LogCallbackError(nameof(ConnectionLifecycleCallback.OnConnectionInitiated), PeerLookup.DeviceIdFor(endpointId), ex)),
                new AdvertisingOptions.Builder()
                    .SetStrategy(_options.ToPlatformStrategy())
                    .SetLowPower(_options.Android.UseLowPower)
                    .SetConnectionType(_options.ToPlatformConnectionType())
                    .Build()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogStartAdvertisingFailed(ex);
            throw new NearbyAdvertisingException("Failed to start advertising.", ex);
        }
    }

    void PlatformStopAdvertising()
    {
        _advertiseClient?.StopAdvertising();
        _advertiseClient?.Dispose();
        _advertiseClient = null;
    }

    internal async Task OnConnectionInitiatedAsync(string endpointId, ConnectionInfo connectionInfo)
    {
        try
        {
            var deviceId = PeerLookup.DeviceIdFor(endpointId);
            var device = PeerLookup.Record(deviceId, connectionInfo.EndpointName);

            if (connectionInfo.IsIncomingConnection)
            {
                LogConnectionRequestReceived(device.Id, device.DisplayName);

                var tcs = RegisterConnectionTcs(deviceId, CancellationToken.None);
                var request = new NearbyConnectionRequest(
                    device,
                    accept: ct =>
                    {
                        AttachConnectionTcsToken(deviceId, ct);

                        return AwaitHandshakeAsync(
                            device,
                            tcs,
                            ConnectionRole.Acceptor,
                            beforeAwait: _ => PlatformRespondToConnectionAsync(device, accept: true),
                            ct);
                    },
                    reject: ct =>
                    {
                        _connectionTcs.TryRemove(deviceId, out _);
                        return PlatformRespondToConnectionAsync(device, accept: false);
                    });

                WriteConnectionRequest(request);
            }
            else
            {
                await PlatformRespondToConnectionAsync(device, accept: true).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            var deviceId = PeerLookup.DeviceIdFor(endpointId);

            LogCallbackError(nameof(OnConnectionInitiatedAsync), deviceId, ex);
            FaultConnectionTcs(deviceId, ex);
        }
    }

    internal void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        try
        {
            var deviceId = PeerLookup.DeviceIdFor(endpointId);

            LogConnectionResult(
                deviceId,
                resolution.Status.StatusCode,
                resolution.Status.StatusMessage ?? string.Empty,
                resolution.Status.IsSuccess);

            if (resolution.Status.IsSuccess)
            {
                if (!PeerLookup.TryGetDevice(deviceId, out var device))
                {
                    FaultConnectionTcs(deviceId, new NearbyException($"Device not found in manager for device '{deviceId}' after successful connection."));
                    return;
                }

                var receiveChannel = NewChannel<NearbyPayload>(singleReader: true);

                var connection = new NearbyConnection(
                    device,
                    receiveChannel,
                    sendBytes: (data, ct) => PlatformSendBytesAsync(deviceId, data, ct),
                    sendFile: (fileUri, progress, ct) => PlatformSendFileAsync(deviceId, fileUri, progress, ct),
                    dispose: () => PlatformDisconnectEndpointAsync(deviceId));

                ResolveConnectionTcs(deviceId, connection);
            }
            else
            {
                if (PeerLookup.Remove(deviceId) is { } lostDevice)
                {
                    WriteDeviceLost(lostDevice);
                }

                FaultConnectionTcs(deviceId, new NearbyException(
                    $"Connection to device '{deviceId}' failed: {resolution.Status.StatusMessage} (code {resolution.Status.StatusCode})."));
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnConnectionResult), PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    internal void OnDisconnected(string endpointId)
    {
        try
        {
            var deviceId = PeerLookup.DeviceIdFor(endpointId);

            LogDeviceDisconnected(deviceId);

            // A GMS callback: the signature is fixed, so the release is tracked rather than awaited.
            ReleaseConnectionFromCallback(deviceId);
            PeerLookup.Remove(deviceId);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnDisconnected), PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    #region Discovery

    async Task PlatformStartDiscoveryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _discoverClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            await _discoverClient.StartDiscoveryAsync(
                _options.ServiceId,
                new DiscoveryCallback(OnEndpointFound, OnEndpointLost),
                new DiscoveryOptions.Builder()
                    .SetStrategy(_options.ToPlatformStrategy())
                    .SetLowPower(_options.Android.UseLowPower)
                    .Build()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogStartDiscoveringFailed(ex);

            throw new NearbyDiscoveryException("Failed to start discovery.", ex);
        }
    }

    void PlatformStopDiscovering()
    {
        _discoverClient?.StopDiscovery();
        _discoverClient?.Dispose();
        _discoverClient = null;
    }

    internal void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        try
        {
            var device = PeerLookup.Record(PeerLookup.DeviceIdFor(endpointId), info.EndpointName);
            LogDeviceFound(device.Id, device.DisplayName);
            WriteDeviceFound(device);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnEndpointFound), PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    internal void OnEndpointLost(string endpointId)
    {
        try
        {
            var deviceId = PeerLookup.DeviceIdFor(endpointId);

            if (_activeConnections.ContainsKey(deviceId))
            {
                if (PeerLookup.TryGetDevice(deviceId, out var existingDevice))
                {
                    LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
                }

                return;
            }

            var device = PeerLookup.Remove(deviceId);
            LogDeviceLost(deviceId, device?.DisplayName);

            if (device is not null)
            {
                WriteDeviceLost(device);
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnEndpointLost), PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    #endregion Discovery

    internal void OnPayloadReceived(string endpointId, Payload payload)
    {
        try
        {
            var deviceId = PeerLookup.DeviceIdFor(endpointId);

            LogPayloadReceived(deviceId, payload.Id, payload.PayloadType);
            _incomingPayloads.TryAdd(payload.Id, (deviceId, payload));
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnPayloadReceived), PeerLookup.DeviceIdFor(endpointId), ex);
        }
    }

    internal async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        try
        {
            var deviceId = PeerLookup.DeviceIdFor(endpointId);

            LogPayloadTransferUpdate(deviceId, update.PayloadId, update.TransferStatus, update.TotalBytes, update.BytesTransferred);

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
                && _activeConnections.TryGetValue(inboundEntry.DeviceId, out var inboundConn))
            {
                inboundConn.InboundProgress?.Report(new NearbyTransferProgress(
                    payloadId: update.PayloadId,
                    bytesTransferred: update.BytesTransferred,
                    totalBytes: update.TotalBytes,
                    NearbyTransferStatus.InProgress));
            }

            if (update.TransferStatus == PayloadTransferUpdate.Status.Success)
            {
                var payloadId = update.PayloadId;

                await _workQueue
                    .Enqueue(deviceId, () => OnIncomingPayloadSuccess(deviceId, payloadId))
                    .ConfigureAwait(false);
            }
            else if (update.TransferStatus is PayloadTransferUpdate.Status.Failure or PayloadTransferUpdate.Status.Canceled
                && _incomingPayloads.TryRemove(update.PayloadId, out var deadEntry))
            {
                LogIncomingPayloadProcessingFailed(deviceId, update.PayloadId);
                DisposeIncomingPayload(deadEntry.Payload);
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnPayloadTransferUpdate), PeerLookup.DeviceIdFor(endpointId), ex);
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
        var copyToken = _activeConnections.TryGetValue(deviceId, out var connection)
            ? connection.DisconnectedToken
            : new CancellationToken(canceled: true);

        NearbyPayload? nearbyPayload = entry.Payload.PayloadType == Payload.Type.File
            ? await CopyFilePayloadAsync(entry.Payload, StagingDirectory, copyToken).ConfigureAwait(false)
            : entry.Payload.AsBytes() is { } bytes
                ? new NearbyBytesPayload(bytes)
                : null;

        if (nearbyPayload is not null)
        {
            WritePayload(deviceId, nearbyPayload);
        }
        else if (!copyToken.IsCancellationRequested)
        {
            // A cancelled copy already logged its own teardown message. Reporting it again as a
            // processing failure would raise a routine disconnect to Error.
            LogIncomingPayloadProcessingFailed(deviceId, payloadId);
        }

        DisposeIncomingPayload(entry.Payload);
    }

    static void DisposeIncomingPayload(Payload payload)
    {
        payload.Close();
        payload.Dispose();
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

            var outputStream = ClaimUniqueDestinationPath(destinationDirectory, fileName);
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
            LogFileCopyCanceled(source, destinationPath ?? destinationDirectory);
            DeletePartialDestination(destinationPath);
            return null;
        }
        catch (Exception ex)
        {
            LogFileCopyFailed(source, destinationPath ?? destinationDirectory, ex);
            DeletePartialDestination(destinationPath);
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
                LogFileDeleteFailed(sourceUri.ToString()!, ex);
            }
        }

        return new NearbyFilePayload(new FileResult(destinationPath));
    }

    async Task PlatformInitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!PeerLookup.TryGetEndpointId(device.Id, out var endpointId))
        {
            FaultConnectionTcs(device.Id, new NearbyException(
                $"Cannot connect: device '{device.DisplayName}' (Id={device.Id}) is not currently visible. Ensure it is actively advertising and within range."));
            return;
        }

        try
        {
            // Must be awaited HERE, not returned directly
            await NearbyClass
                .GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
                .RequestConnectionAsync(
                    _options.DisplayName,
                    endpointId,
                    new AdvertiseCallback(
                        OnConnectionInitiatedAsync,
                        OnConnectionResult,
                        OnDisconnected,
                        (endpointId, ex) => LogCallbackError(nameof(ConnectionLifecycleCallback.OnConnectionInitiated), PeerLookup.DeviceIdFor(endpointId), ex))).ConfigureAwait(false);
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
                LogFailedToClearStaleConnectionState(device.Id, disconnectEx);
            }

            FaultConnectionTcs(device.Id, new NearbyException(
                $"Failed to initiate connection to device '{device.Id}'.", ex));
        }
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        if (!PeerLookup.TryGetEndpointId(device.Id, out var endpointId))
        {
            throw new NearbyException(
                $"Cannot respond to the connection request from device '{device.Id}': the platform no longer tracks it.");
        }

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return accept
            ? client.AcceptConnectionAsync(endpointId, new ConnectionCallback(
                OnPayloadReceived,
                OnPayloadTransferUpdate,
                (callbackEndpointId, ex) => LogCallbackError(nameof(PayloadCallback.OnPayloadTransferUpdate), PeerLookup.DeviceIdFor(callbackEndpointId), ex)))
            : client.RejectConnectionAsync(endpointId);
    }

    async Task PlatformAbandonConnectAsync(NearbyDevice device)
    {
        try
        {
            await PlatformDisconnectEndpointAsync(device.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogAbandonConnectError(device.Id, ex);
        }
    }

    async ValueTask PlatformDisconnectEndpointAsync(string deviceId)
    {
        LogDisconnecting(deviceId, PeerLookup.TryGetDevice(deviceId, out var d)
            ? d.DisplayName
            : null);

        if (PeerLookup.TryGetEndpointId(deviceId, out var endpointId))
        {
            var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
            client.DisconnectFromEndpoint(endpointId);
        }

        await ReleaseConnectionAsync(deviceId).ConfigureAwait(false);
        PeerLookup.Remove(deviceId);
    }

    async Task PlatformSendBytesAsync(
        string deviceId,
        byte[] data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_activeConnections.ContainsKey(deviceId) || !PeerLookup.TryGetEndpointId(deviceId, out var endpointId))
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
            LogSendBytesFailed(deviceId, ex);
            throw new NearbyTransferException(
                $"Failed to send bytes to device '{deviceId}'.", ex);
        }
    }

    async Task PlatformSendFileAsync(
        string deviceId,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_activeConnections.ContainsKey(deviceId) || !PeerLookup.TryGetEndpointId(deviceId, out var endpointId))
        {
            throw new NearbyException(
                $"Cannot send file: no active connection for device '{deviceId}'.");
        }

        using var androidUri = TryCreateUri(uri);

        if (androidUri is null)
        {
            LogInvalidFileUri(uri);
            throw new NearbyTransferException("Cannot send file: the URI is not a valid or supported scheme. Use a file:// or content:// URI.");
        }

        var filePayload = BuildFilePayload(androidUri) ?? throw new NearbyTransferException("Cannot send file: failed to open the file descriptor for the given URI.");
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var transfer = new OutgoingTransfer(progress, _options.TransferInactivityTimeout, TimeProvider);

        _outgoingTransfers.TryAdd(filePayload.Id, transfer);

        async Task CancelPayloadLoggedAsync()
        {
            try
            {
                await client.CancelPayloadAsync(filePayload.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWriteError(nameof(PlatformSendFileAsync), deviceId, ex);
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

        try
        {
            await client.SendPayloadAsync(endpointId, filePayload).ConfigureAwait(false);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(() => _ = CancelPayloadLoggedAsync());
            await transfer.Completion.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Report(NearbyTransferStatus.Canceled);
            throw;
        }
        catch (OperationCanceledException) when (transfer.InactivityToken.IsCancellationRequested)
        {
            Report(NearbyTransferStatus.Failure);

            throw TransferInactivityTimeoutException(deviceId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not NearbyException)
        {
            Report(NearbyTransferStatus.Failure);

            LogSendFileFailed(deviceId, null, ex);
            throw new NearbyTransferException(
                $"Failed to send file to device '{deviceId}'.", ex);
        }
        finally
        {
            _outgoingTransfers.TryRemove(filePayload.Id, out _);
            transfer.Dispose();
            filePayload.Close();
            filePayload.Dispose();

            // A terminal GMS update can fault transfer.Completion after this caller has already
            // left the await on one of the catch paths above, leaving the fault unobserved and
            // surfacing later on the finalizer thread. Observing it here retires that. The iOS
            // sibling does the same at the end of its own PlatformSendFileAsync.
            _ = transfer.Completion.Exception;
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
            LogBuildFilePayloadFailed(ex);
        }

        return null;
    }

    async Task<NearbyAvailability> PlatformCheckAvailabilityAsync(CancellationToken cancellationToken)
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
            LogAvailabilityCheckPartiallyFailed(nameof(NearbyAvailability.BluetoothDisabled), ex);
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
            LogAvailabilityCheckPartiallyFailed(nameof(NearbyAvailability.WifiDisabled), ex);
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

    partial void PlatformReleaseConnection(string peerId)
    {
        // Runs after PlatformQuiesceConnectionAsync, so no copy is still reading these.
        foreach (var (payloadId, entry) in _incomingPayloads)
        {
            if (entry.DeviceId == peerId
                && _incomingPayloads.TryRemove(payloadId, out var removed))
            {
                DisposeIncomingPayload(removed.Payload);
            }
        }
    }

    internal static partial string StagingDirectory => Path.Combine(FileSystem.CacheDirectory, StagingDirectoryName);

    void PlatformSweepStaging() => SweepStagingDirectory(StagingDirectory);

    void PlatformDispose()
    {
        foreach (var (_, entry) in _incomingPayloads)
        {
            DisposeIncomingPayload(entry.Payload);
        }
        _incomingPayloads.Clear();

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