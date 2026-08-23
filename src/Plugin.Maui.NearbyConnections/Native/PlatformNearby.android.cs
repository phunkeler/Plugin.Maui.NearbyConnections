using Android.Content;
using AndroidUri = Android.Net.Uri;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, OutgoingTransfer> _outgoingTransfers = [];
    readonly Dictionary<string, Task> _payloadCompletionChains = [];

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
                    (endpointId, ex) => LogCallbackError(nameof(ConnectionLifecycleCallback.OnConnectionInitiated), endpointId, ex)),
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
            var device = Peers.Record(endpointId, connectionInfo.EndpointName);

            if (connectionInfo.IsIncomingConnection)
            {
                LogConnectionRequestReceived(device.Id, device.DisplayName);

                var tcs = RegisterConnectionTcs(endpointId, CancellationToken.None);
                var request = new NearbyConnectionRequest(
                    device,
                    accept: ct =>
                    {
                        AttachConnectionTcsToken(endpointId, ct);

                        return AwaitHandshakeAsync(
                            device,
                            tcs,
                            ConnectionRole.Acceptor,
                            beforeAwait: _ => PlatformRespondToConnectionAsync(device, accept: true),
                            ct);
                    },
                    reject: ct =>
                    {
                        _connectionTcs.TryRemove(endpointId, out _);
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
            LogCallbackError(nameof(OnConnectionInitiatedAsync), endpointId, ex);
            FaultConnectionTcs(endpointId, ex);
        }
    }

    internal void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        try
        {
            LogConnectionResult(
                endpointId,
                resolution.Status.StatusCode,
                resolution.Status.StatusMessage ?? string.Empty,
                resolution.Status.IsSuccess);

            if (resolution.Status.IsSuccess)
            {
                if (!Peers.TryGetDevice(endpointId, out var device))
                {
                    FaultConnectionTcs(endpointId, new NearbyException($"Device not found in manager for endpoint '{endpointId}' after successful connection."));
                    return;
                }

                var receiveChannel = NewChannel<NearbyPayload>(singleReader: true);

                var connection = new NearbyConnection(
                    device,
                    receiveChannel,
                    sendBytes: (data, ct) => PlatformSendBytesAsync(endpointId, data, ct),
                    sendFile: (fileUri, progress, ct) => PlatformSendFileAsync(endpointId, fileUri, progress, ct),
                    dispose: () =>
                    {
                        PlatformDisconnectEndpointAsync(endpointId);
                        return ValueTask.CompletedTask;
                    });

                ResolveConnectionTcs(endpointId, connection);
            }
            else
            {
                if (Peers.Remove(endpointId) is { } lostDevice)
                {
                    WriteDeviceLost(lostDevice);
                }

                FaultConnectionTcs(endpointId, new NearbyException(
                    $"Connection to endpoint '{endpointId}' failed: {resolution.Status.StatusMessage} (code {resolution.Status.StatusCode})."));
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnConnectionResult), endpointId, ex);
        }
    }

    internal void OnDisconnected(string endpointId)
    {
        try
        {
            LogDeviceDisconnected(endpointId);
            ReleaseConnection(endpointId);
            Peers.Remove(endpointId);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnDisconnected), endpointId, ex);
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
            var device = Peers.Record(endpointId, info.EndpointName);
            LogDeviceFound(device.Id, device.DisplayName);
            WriteDeviceFound(device);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnEndpointFound), endpointId, ex);
        }
    }

    internal void OnEndpointLost(string endpointId)
    {
        try
        {
            if (_activeConnections.ContainsKey(endpointId))
            {
                if (Peers.TryGetDevice(endpointId, out var existingDevice))
                {
                    LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
                }

                return;
            }

            var device = Peers.Remove(endpointId);
            LogDeviceLost(endpointId, device?.DisplayName);

            if (device is not null)
            {
                WriteDeviceLost(device);
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnEndpointLost), endpointId, ex);
        }
    }

    #endregion Discovery

    internal void OnPayloadReceived(string endpointId, Payload payload)
    {
        try
        {
            LogPayloadReceived(endpointId, payload.Id, payload.PayloadType);
            _incomingPayloads.TryAdd(payload.Id, (endpointId, payload));
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnPayloadReceived), endpointId, ex);
        }
    }

    internal async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        try
        {
            LogPayloadTransferUpdate(endpointId, update.PayloadId, update.TransferStatus, update.TotalBytes, update.BytesTransferred);

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
                && _activeConnections.TryGetValue(inboundEntry.EndpointId, out var inboundConn))
            {
                inboundConn.InboundProgress?.Report(new NearbyTransferProgress(
                    payloadId: update.PayloadId,
                    bytesTransferred: update.BytesTransferred,
                    totalBytes: update.TotalBytes,
                    NearbyTransferStatus.InProgress));
            }

            if (update.TransferStatus == PayloadTransferUpdate.Status.Success)
            {
                await ChainPayloadCompletion(endpointId, update.PayloadId).ConfigureAwait(false);
            }
            else if (update.TransferStatus is PayloadTransferUpdate.Status.Failure or PayloadTransferUpdate.Status.Canceled
                && _incomingPayloads.TryRemove(update.PayloadId, out var deadEntry))
            {
                LogIncomingPayloadProcessingFailed(endpointId, update.PayloadId);
                DisposeIncomingPayload(deadEntry.Payload);
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnPayloadTransferUpdate), endpointId, ex);
        }
    }

    Task ChainPayloadCompletion(string endpointId, long payloadId)
    {
        Task chained;

        lock (_payloadCompletionChains)
        {
            var previous = _payloadCompletionChains.GetValueOrDefault(endpointId, Task.CompletedTask);
            chained = ContinueAsync(previous);
            _payloadCompletionChains[endpointId] = chained;
        }

        return chained;

        async Task ContinueAsync(Task previous)
        {
            await previous.ConfigureAwait(false);

            try
            {
                await OnIncomingPayloadSuccess(endpointId, payloadId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogCallbackError(nameof(OnIncomingPayloadSuccess), endpointId, ex);
            }
        }
    }

    async Task OnIncomingPayloadSuccess(string endpointId, long payloadId)
    {
        if (!_incomingPayloads.TryRemove(payloadId, out var entry))
        {
            return;
        }

        // The copy is bounded by the connection's own teardown: DisconnectedToken is cancelled by
        // CompleteReceive, and DisposeAsync disposes every active connection, so disposing the
        // session cancels an in-flight copy too. Without a live connection the copied file has
        // nowhere to go, so skip the work rather than copy a file WritePayload will drop.
        var copyToken = _activeConnections.TryGetValue(endpointId, out var connection)
            ? connection.DisconnectedToken
            : new CancellationToken(canceled: true);

        NearbyPayload? nearbyPayload = entry.Payload.PayloadType == Payload.Type.File
            ? await CopyFilePayloadAsync(entry.Payload, StagingDirectory, copyToken).ConfigureAwait(false)
            : entry.Payload.AsBytes() is { } bytes
                ? new NearbyBytesPayload(bytes)
                : null;

        if (nearbyPayload is not null)
        {
            WritePayload(endpointId, nearbyPayload);
        }
        else if (!copyToken.IsCancellationRequested)
        {
            // A cancelled copy already logged its own teardown message. Reporting it again as a
            // processing failure would raise a routine disconnect to Error.
            LogIncomingPayloadProcessingFailed(endpointId, payloadId);
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
        var sourceUri = payload.AsFile()?.AsUri();

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

        try
        {
            // Must be awaited HERE, not returned directly
            await NearbyClass
                .GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
                .RequestConnectionAsync(
                    _options.DisplayName,
                    device.Id,
                    new AdvertiseCallback(
                        OnConnectionInitiatedAsync,
                        OnConnectionResult,
                        OnDisconnected,
                        (endpointId, ex) => LogCallbackError(nameof(ConnectionLifecycleCallback.OnConnectionInitiated), endpointId, ex))).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            try
            {
                NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
                    .DisconnectFromEndpoint(device.Id);
            }
            catch (Exception disconnectEx)
            {
                LogFailedToClearStaleConnectionState(device.Id, disconnectEx);
            }

            FaultConnectionTcs(device.Id, new NearbyException(
                $"Failed to initiate connection to endpoint '{device.Id}'.", ex));
        }
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return accept
            ? client.AcceptConnectionAsync(device.Id, new ConnectionCallback(
                OnPayloadReceived,
                OnPayloadTransferUpdate,
                (endpointId, ex) => LogCallbackError(nameof(PayloadCallback.OnPayloadTransferUpdate), endpointId, ex)))
            : client.RejectConnectionAsync(device.Id);
    }

    Task PlatformAbandonConnectAsync(NearbyDevice device)
    {
        try
        {
            PlatformDisconnectEndpointAsync(device.Id);
        }
        catch (Exception ex)
        {
            LogAbandonConnectError(device.Id, ex);
        }

        return Task.CompletedTask;
    }

    void PlatformDisconnectEndpointAsync(string endpointId)
    {
        LogDisconnecting(endpointId, Peers.TryGetDevice(endpointId, out var d)
            ? d.DisplayName
            : null);

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        client.DisconnectFromEndpoint(endpointId);
        ReleaseConnection(endpointId);
        Peers.Remove(endpointId);
    }

    async Task PlatformSendBytesAsync(
        string endpointId,
        byte[] data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_activeConnections.ContainsKey(endpointId))
        {
            throw new NearbyException(
                $"Cannot send bytes: no active connection for endpoint '{endpointId}'.");
        }

        using var payload = Payload.FromBytes(data);
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            await client.SendPayloadAsync(endpointId, payload).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendBytesFailed(endpointId, ex);
            throw new NearbyTransferException(
                $"Failed to send bytes to endpoint '{endpointId}'.", ex);
        }
    }

    async Task PlatformSendFileAsync(
        string endpointId,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_activeConnections.ContainsKey(endpointId))
        {
            throw new NearbyException(
                $"Cannot send file: no active connection for endpoint '{endpointId}'.");
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
                LogWriteError(nameof(PlatformSendFileAsync), endpointId, ex);
            }
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
            progress?.Report(new NearbyTransferProgress(
                payloadId: filePayload.Id,
                bytesTransferred: 0,
                totalBytes: 0,
                NearbyTransferStatus.Canceled));
            throw;
        }
        catch (OperationCanceledException) when (transfer.InactivityToken.IsCancellationRequested)
        {
            progress?.Report(new NearbyTransferProgress(
                payloadId: filePayload.Id,
                bytesTransferred: 0,
                totalBytes: 0,
                NearbyTransferStatus.Failure));

            throw TransferInactivityTimeoutException(endpointId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not NearbyException)
        {
            progress?.Report(new NearbyTransferProgress(
                payloadId: filePayload.Id,
                bytesTransferred: 0,
                totalBytes: 0,
                NearbyTransferStatus.Failure));

            LogSendFileFailed(endpointId, null, ex);
            throw new NearbyTransferException(
                $"Failed to send file to endpoint '{endpointId}'.", ex);
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
        foreach (var (payloadId, entry) in _incomingPayloads)
        {
            if (entry.EndpointId == peerId
                && _incomingPayloads.TryRemove(payloadId, out var removed))
            {
                DisposeIncomingPayload(removed.Payload);
            }
        }

        lock (_payloadCompletionChains)
        {
            _payloadCompletionChains.Remove(peerId);
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

        lock (_payloadCompletionChains)
        {
            _payloadCompletionChains.Clear();
        }
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