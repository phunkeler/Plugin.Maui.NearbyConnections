using Android.Content;
using AndroidUri = Android.Net.Uri;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    IConnectionsClient? _advertiseClient;
    IConnectionsClient? _discoverClient;

    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, OutgoingTransfer> _outgoingTransfers = [];

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
                    .Build());
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

                // No caller token exists yet — the request arrives on a GMS callback, not from a
                // consumer call. AcceptAsync attaches the real one below.
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
                await PlatformRespondToConnectionAsync(device, accept: true);
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
                Peers.Remove(endpointId);
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
                    .Build());
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
                await OnIncomingPayloadSuccess(endpointId, update.PayloadId);
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnPayloadTransferUpdate), endpointId, ex);
        }
    }

    async Task OnIncomingPayloadSuccess(string endpointId, long payloadId)
    {
        if (!_incomingPayloads.TryRemove(payloadId, out var entry))
        {
            return;
        }

        NearbyPayload? nearbyPayload = entry.Payload.PayloadType == Payload.Type.File
            ? await CopyFilePayloadAsync(entry.Payload, _options.ReceivedFilesDirectory, CancellationToken.None)
            : entry.Payload.AsBytes() is { } bytes
                ? new NearbyBytesPayload(bytes)
                : null;

        if (nearbyPayload is not null)
        {
            WritePayload(endpointId, nearbyPayload);
        }
        else
        {
            LogIncomingPayloadProcessingFailed(endpointId, payloadId);
        }

        entry.Payload.Dispose();
    }

    async Task<NearbyFilePayload?> CopyFilePayloadAsync(Payload payload, string destinationDirectory, CancellationToken cancellationToken)
    {
        var sourceUri = payload.AsFile()?.AsUri();

        if (sourceUri is null)
        {
            return null;
        }

        var fileName = ResolveResourceName(sourceUri);
        var destinationPath = ResolveUniqueDestinationPath(destinationDirectory, fileName);

        try
        {
            using var inputStream = Application.Context.ContentResolver?.OpenInputStream(sourceUri);

            if (inputStream is null)
            {
                return null;
            }

            // FileMode.Create, not File.OpenWrite: OpenWrite uses OpenOrCreate and does NOT
            // truncate, so receiving a smaller file over an existing same-named one left the
            // previous file's trailing bytes in place — a silently corrupted file delivered to
            // the app with no error. iOS uses File.Copy(overwrite: true), which does truncate.
            using var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
            await inputStream.CopyToAsync(outputStream, cancellationToken);
        }
        catch (Exception ex)
        {
            LogFileCopyFailed(sourceUri.ToString()!, destinationPath, ex);
            return null;
        }
        finally
        {
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
            // Must be awaited HERE, not returned directly — RequestConnectionAsync's
            // Task can fault asynchronously (the ApiException below arrives after
            // this call already returned a pending Task, not during its
            // construction), so a try/catch around the call expression alone
            // never observes it. Returning the Task un-awaited let the fault
            // propagate to whatever later awaited it (ConnectAsync's own await),
            // bypassing this catch entirely — confirmed by the exact same crash
            // still occurring after a first attempt at this fix that didn't
            // await here.
            await NearbyClass
                .GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
                .RequestConnectionAsync(
                    _options.DisplayName,
                    device.Id,
                    new AdvertiseCallback(
                        OnConnectionInitiatedAsync,
                        OnConnectionResult,
                        OnDisconnected,
                        (endpointId, ex) => LogCallbackError(nameof(ConnectionLifecycleCallback.OnConnectionInitiated), endpointId, ex)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // RequestConnectionAsync can fail (e.g. Google Play Services'
            // ApiException STATUS_ALREADY_CONNECTED_TO_ENDPOINT). Left
            // unguarded, this exception propagated out of ConnectAsync's await
            // and crashed the whole app when a caller's own catch clause only
            // handled NearbyException. Fault the already-registered
            // TCS instead, consistent with how every other platform failure in
            // this file is surfaced (see PlatformStartAdvertisingAsync,
            // OnConnectionResult), so callers get a normal, typed, catchable
            // failure instead of an unhandled platform exception.
            //
            // STATUS_ALREADY_CONNECTED_TO_ENDPOINT specifically means Google
            // Play Services' Nearby Connections client (a system-level
            // process, not part of this app's object graph) still considers
            // this endpoint connected from a PRIOR attempt that never called
            // DisconnectFromEndpoint — e.g. this ConnectAsync call itself
            // previously threw/was cancelled before a NearbyConnection object
            // (whose disposal is normally what triggers
            // PlatformDisconnectEndpointAsync) was ever created. That GMS-side
            // state is independent of the app's own belief about whether
            // it's connected, and persists until explicitly cleared — per
            // Google's own reference implementations, the fix is to always
            // call DisconnectFromEndpoint on a failed connection attempt too,
            // not just on an explicit user-initiated disconnect, so a
            // subsequent retry doesn't hit the same stuck state.
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

    /// <summary>
    /// Clears Google Play Services' view of a connection attempt that timed out, so the endpoint is
    /// not left marked connected from an attempt this app has already given up on.
    /// </summary>
    /// <remarks>
    /// Without this, GMS — a system process, independent of this app's object graph — still
    /// considers the endpoint connected, and the next <c>ConnectAsync</c> fails with
    /// <c>STATUS_ALREADY_CONNECTED_TO_ENDPOINT</c>. One un-cleaned timeout would poison every
    /// subsequent retry to that device.
    /// </remarks>
    Task PlatformAbandonConnectAsync(NearbyDevice device)
    {
        try
        {
            PlatformDisconnectEndpointAsync(device.Id);
        }
        catch (Exception ex)
        {
            // Best-effort cleanup on a path that is already failing: the caller is about to get a
            // timeout exception, and masking it with a teardown failure would hide the real cause.
            LogAbandonConnectError(device.Id, ex);
        }

        return Task.CompletedTask;
    }

    void PlatformDisconnectEndpointAsync(string endpointId)
    {
        LogDisconnecting(endpointId, Peers.TryGetDevice(endpointId, out var d) ? d.DisplayName : null);

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

        await client.SendPayloadAsync(endpointId, payload);
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

        try
        {
            await client.SendPayloadAsync(endpointId, filePayload);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(() => _ = client.CancelPayloadAsync(filePayload.Id));
            await transfer.Completion.WaitAsync(linkedCts.Token);
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
        finally
        {
            _outgoingTransfers.TryRemove(filePayload.Id, out _);
            transfer.Dispose();
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

    /// <summary>
    /// Reports what would stop advertising or discovery from working right now.
    /// </summary>
    /// <remarks>
    /// Every condition is evaluated, rather than returning at the first problem, so the caller can
    /// tell the user everything that needs fixing in one prompt instead of one per attempt.
    /// Permission checks use the .NET MAUI <c>Permissions</c> API's <c>CheckStatusAsync</c>, which
    /// reports status without prompting — this method must never surface a system dialog.
    /// </remarks>
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

    /// <summary>
    /// Checks the runtime permissions Nearby Connections needs on this API level, without
    /// prompting. Install-time permissions are not checked: they are granted by virtue of being
    /// declared, which the package does on the consumer's behalf.
    /// </summary>
    static async Task<bool> ArePermissionsGrantedAsync()
    {
        // API 31+ replaced the location requirement with the granular BLUETOOTH_* permissions;
        // API 33+ added NEARBY_WIFI_DEVICES. The boundaries below follow the running OS, because
        // that is what determines which permissions the device can actually grant.
        if (await Permissions.CheckStatusAsync<Permissions.Bluetooth>().ConfigureAwait(false) != PermissionStatus.Granted)
        {
            return false;
        }

        // Below API 31, Permissions.Bluetooth resolves to an EMPTY permission set and so returns
        // Granted having checked nothing: its ACCESS_FINE_LOCATION branch is gated on the app's
        // target SDK being <= 30, which no current MAUI app satisfies. Location is the real gate on
        // those devices, so it must be checked explicitly or the preflight reports a vacuous Ready.
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false) == PermissionStatus.Granted;
        }

        // On 31+ BLUETOOTH_SCAN covers discovery, so location is deliberately NOT required — asking
        // for it here would report a permission the app has no reason to hold. NEARBY_WIFI_DEVICES
        // does not exist before 33, so it is only checked from there up.
        return !OperatingSystem.IsAndroidVersionAtLeast(33)
            || await Permissions.CheckStatusAsync<Permissions.NearbyWifiDevices>().ConfigureAwait(false) == PermissionStatus.Granted;
    }

    void PlatformDispose()
    {
        foreach (var (_, entry) in _incomingPayloads)
        {
            entry.Payload.Dispose();
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
                await onConnectionInitiated(p0, p1);
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
                await onPayloadTransferUpdate(p0, p1);
            }
            catch (Exception ex)
            {
                onError?.Invoke(p0, ex);
            }
        }
    }
}