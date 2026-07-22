using System.Threading.Channels;
using Android.Content;
using AndroidUri = Android.Net.Uri;
using Path = System.IO.Path;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    // Thread-safety of these fields depends entirely on the tier-2 guarantee, enforced by
    // NearbyAdvertiser/NearbyDiscoverer, that at most one AdvertiseAsync/DiscoverAsync
    // invocation is ever in flight at a time. A future change to tier-1 or tier-2 that
    // reintroduces overlapping invocations would reintroduce a native use-after-dispose race
    // here (see the fix for the fire-and-forget RunLoopAsync race). Do not add concurrent
    // callers of these fields without re-establishing that guarantee.
    IConnectionsClient? _advertiseClient;
    IConnectionsClient? _discoverClient;

    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, OutgoingTransfer> _outgoingTransfers = [];

    #region Advertising

    Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _advertiseClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            return _advertiseClient.StartAdvertisingAsync(
                Options.DisplayName,
                Options.ServiceId,
                new AdvertiseCallback(OnConnectionInitiatedAsync, OnConnectionResult, OnDisconnected, LogOnConnectionInitiatedError),
                new AdvertisingOptions.Builder()
                    .SetStrategy(Options.Strategy)
                    .SetLowPower(Options.UseLowPower)
                    .Build());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _advertiseChannel.Writer.TryComplete(new NearbyAdvertisingException("Failed to start advertising.", ex));
            return Task.CompletedTask;
        }
    }

    void PlatformStopAdvertising()
    {
        _advertiseClient?.StopAdvertising();
        _advertiseClient?.Dispose();
        _advertiseClient = null;
    }

    /// <summary>
    /// "A basic encrypted channel has been created between you and the endpoint.
    /// Both sides are now asked if they wish to accept or reject the connection before any data can be sent over this channel."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectioninitiated-string-endpointid,-connectioninfo-connectioninfo">developers.google.com</see>
    /// </summary>
    async Task OnConnectionInitiatedAsync(string endpointId, ConnectionInfo connectionInfo)
    {
        try
        {
            var device = _deviceManager.RecordDeviceFound(endpointId, connectionInfo.EndpointName);

            if (connectionInfo.IsIncomingConnection)
            {
                LogConnectionRequestReceived(device.Id, device.DisplayName);

                // Register a TCS so that AcceptAsync can await the connection result.
                var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                _connectionTcs[endpointId] = (tcs, CancellationToken.None);

                var request = new NearbyConnectionRequest(
                    device,
                    acceptFactory: async ct =>
                    {
                        await PlatformRespondToConnectionAsync(device, accept: true);
                        return await tcs.Task.WaitAsync(ct);
                    },
                    rejectFactory: ct =>
                    {
                        _connectionTcs.TryRemove(endpointId, out _);
                        return PlatformRespondToConnectionAsync(device, accept: false);
                    });

                WriteConnectionRequest(request);
            }
            else
            {
                // Outbound (discoverer side): auto-accept at the protocol level.
                await PlatformRespondToConnectionAsync(device, accept: true);
            }
        }
        catch (Exception ex)
        {
            LogOnConnectionInitiatedError(endpointId, ex);
            FaultConnectionTcs(endpointId, ex);
        }
    }

    /// <summary>
    /// "Called after both sides have either accepted or rejected the connection.
    /// If the ConnectionResolution's status is CommonStatusCodes.SUCCESS, both sides have
    /// accepted the connection and may now send Payloads to each other. Otherwise, the connection was rejected."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectionresult-string-endpointid,-connectionresolution-resolution">developers.google.com</see>
    /// </summary>
    public void OnConnectionResult(string endpointId, ConnectionResolution resolution)
    {
        try
        {
            LogConnectionResult(endpointId, resolution.Status.StatusCode, resolution.Status.StatusMessage ?? string.Empty, resolution.Status.IsSuccess);

            if (resolution.Status.IsSuccess)
            {
                if (!_deviceManager.TryGetDevice(endpointId, out var device))
                {
                    FaultConnectionTcs(endpointId, new NearbyConnectionsException($"Device not found in manager for endpoint '{endpointId}' after successful connection."));
                    return;
                }

                var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                });

                var connection = new NearbyConnection(
                    device,
                    receiveChannel,
                    sendBytesFactory: (data, ct) => new ValueTask(PlatformSendBytesAsync(endpointId, data, ct)),
                    sendFileFactory: (fileUri, progress, ct) => PlatformSendFileAsync(endpointId, fileUri, progress, ct),
                    disposeFactory: () =>
                    {
                        PlatformDisconnectEndpointAsync(endpointId);
                        return ValueTask.CompletedTask;
                    });

                ResolveConnectionTcs(endpointId, connection);
            }
            else
            {
                _deviceManager.RemoveDevice(endpointId);
                FaultConnectionTcs(endpointId, new NearbyConnectionsException(
                    $"Connection to endpoint '{endpointId}' failed: {resolution.Status.StatusMessage} (code {resolution.Status.StatusCode})."));
            }
        }
        catch (Exception ex)
        {
            LogOnConnectionResultError(endpointId, ex);
        }
    }

    /// <summary>
    /// "Called when a remote endpoint is disconnected or has become unreachable."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-ondisconnected-string-endpointid">developers.google.com</see>
    /// </summary>
    public void OnDisconnected(string endpointId)
    {
        try
        {
            LogDeviceDisconnected(endpointId);

            if (_activeConnections.TryRemove(endpointId, out var connection))
            {
                connection.CompleteReceive();
            }

            _deviceManager.RemoveDevice(endpointId);
        }
        catch (Exception ex)
        {
            LogOnDisconnectedError(endpointId, ex);
        }
    }

    #endregion Advertising

    #region Discovery

    async Task PlatformStartDiscoveringAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _discoverClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        try
        {
            await _discoverClient.StartDiscoveryAsync(
                Options.ServiceId,
                new DiscoveryCallback(OnEndpointFound, OnEndpointLost),
                new DiscoveryOptions.Builder()
                    .SetStrategy(Options.Strategy)
                    .SetLowPower(Options.UseLowPower)
                    .Build());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _discoverChannel.Writer.TryComplete(new NearbyDiscoveryException("Failed to start discovery.", ex));
        }
    }

    void PlatformStopDiscovering()
    {
        _discoverClient?.StopDiscovery();
        _discoverClient?.Dispose();
        _discoverClient = null;
    }

    public void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        try
        {
            var device = _deviceManager.RecordDeviceFound(endpointId, info.EndpointName);

            LogDeviceFound(device.Id, device.DisplayName);

            WriteDeviceFound(device);
        }
        catch (Exception ex)
        {
            LogOnEndpointFoundError(endpointId, ex);
        }
    }

    public void OnEndpointLost(string endpointId)
    {
        try
        {
            if (_activeConnections.ContainsKey(endpointId))
            {
                if (_deviceManager.TryGetDevice(endpointId, out var existingDevice))
                {
                    LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
                }
                return;
            }

            var device = _deviceManager.RemoveDevice(endpointId);

            LogDeviceLost(endpointId, device?.DisplayName);

            if (device is not null)
            {
                WriteDeviceLost(device);
            }
        }
        catch (Exception ex)
        {
            LogOnEndpointLostError(endpointId, ex);
        }
    }

    #endregion Discovery

    void OnPayloadReceived(string endpointId, Payload payload)
    {
        try
        {
            LogPayloadReceived(endpointId, payload.Id, payload.PayloadType);

            _incomingPayloads.TryAdd(payload.Id, (endpointId, payload));
        }
        catch (Exception ex)
        {
            LogOnPayloadReceivedError(endpointId, ex);
        }
    }

    async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
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
            LogOnPayloadTransferUpdateError(endpointId, ex);
        }
    }

    async Task OnIncomingPayloadSuccess(string endpointId, long payloadId)
    {
        if (!_incomingPayloads.TryRemove(payloadId, out var entry))
        {
            return;
        }

        NearbyPayload? nearbyPayload = entry.Payload.PayloadType == Payload.Type.File
            ? await CopyFilePayloadAsync(entry.Payload, Options.ReceivedFilesDirectory, CancellationToken.None)
            : entry.Payload.AsBytes() is { } bytes
                ? new BytesPayload(bytes)
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

    async Task<FilePayload?> CopyFilePayloadAsync(Payload payload, string destinationDirectory, CancellationToken cancellationToken)
    {
        var sourceUri = payload.AsFile()?.AsUri();

        if (sourceUri is null)
        {
            return null;
        }

        var fileName = ResolveResourceName(sourceUri);
        var destinationPath = Path.Combine(destinationDirectory, fileName);

        try
        {
            using var inputStream = Application.Context.ContentResolver?.OpenInputStream(sourceUri);

            if (inputStream is null)
            {
                return null;
            }

            using var outputStream = File.OpenWrite(destinationPath);
            await inputStream.CopyToAsync(outputStream, cancellationToken);
        }
        catch (Exception ex)
        {
            LogFileCopyFailed(sourceUri.ToString()!, destinationPath, ex.Message);
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
                LogFileDeleteFailed(sourceUri.ToString()!, ex.Message);
            }
        }

        return new FilePayload(new FileResult(destinationPath));
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
                    Options.DisplayName,
                    device.Id,
                    new AdvertiseCallback(
                        OnConnectionInitiatedAsync,
                        OnConnectionResult,
                        OnDisconnected,
                        LogOnConnectionInitiatedError));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // RequestConnectionAsync can fail (e.g. Google Play Services'
            // ApiException STATUS_ALREADY_CONNECTED_TO_ENDPOINT). Left
            // unguarded, this exception propagated out of ConnectAsync's await
            // and crashed the whole app when a caller's own catch clause only
            // handled NearbyConnectionsException. Fault the already-registered
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

            FaultConnectionTcs(device.Id, new NearbyConnectionsException(
                $"Failed to initiate connection to endpoint '{device.Id}'.", ex));
        }
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return accept
            ? client.AcceptConnectionAsync(device.Id, new ConnectionCallback(OnPayloadReceived, OnPayloadTransferUpdate, LogOnPayloadTransferUpdateError))
            : client.RejectConnectionAsync(device.Id);
    }

    void PlatformDisconnectEndpointAsync(string endpointId)
    {
        LogDisconnecting(endpointId, _deviceManager.TryGetDevice(endpointId, out var d) ? d.DisplayName : null);

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        client.DisconnectFromEndpoint(endpointId);

        if (_activeConnections.TryRemove(endpointId, out var conn))
        {
            conn.CompleteReceive();
        }

        _deviceManager.RemoveDevice(endpointId);
    }

    Task PlatformSendBytesAsync(
        string endpointId,
        byte[] data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_activeConnections.ContainsKey(endpointId))
        {
            throw new NearbyConnectionsException(
                $"Cannot send bytes: no active connection for endpoint '{endpointId}'.");
        }

        using var payload = Payload.FromBytes(data);
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return client.SendPayloadAsync(endpointId, payload);
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
            throw new NearbyConnectionsException(
                $"Cannot send file: no active connection for endpoint '{endpointId}'.");
        }

        using var androidUri = TryCreateUri(uri);

        if (androidUri is null)
        {
            LogInvalidFileUri(uri);
            throw new InvalidOperationException($"Cannot send file: the URI is not a valid or supported scheme. Use a file:// or content:// URI.");
        }

        var filePayload = BuildFilePayload(androidUri) ?? throw new InvalidOperationException($"Cannot send file: failed to open the file descriptor for the given URI.");
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var transfer = new OutgoingTransfer(progress, Options.TransferInactivityTimeout);

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

            LogSendFileTimeout(endpointId, null, Options.TransferInactivityTimeout.TotalSeconds);

            throw new NearbyTransferTimeoutException(
                $"Transfer stalled: no progress received for {Options.TransferInactivityTimeout}.");
        }
        finally
        {
            _outgoingTransfers.TryRemove(filePayload.Id, out _);
            transfer.Dispose();
            filePayload.Dispose();
        }
    }

    static AndroidUri? TryCreateUri(string fileUri)
    {
        if (string.IsNullOrWhiteSpace(fileUri))
        {
            return null;
        }

        try
        {
            AndroidUri? uri;

            if (Path.IsPathRooted(fileUri))
            {
                using var file = new Java.IO.File(fileUri);
                uri = AndroidUri.FromFile(file);
            }
            else
            {
                uri = AndroidUri.Parse(fileUri);
            }

            return IsSupportedScheme(uri)
                ? uri
                : null;
        }
        catch
        {
            return null;
        }
    }

    static bool IsSupportedScheme(AndroidUri? uri)
        => uri?.Scheme is { } scheme
            && (scheme.Equals(ContentResolver.SchemeFile, StringComparison.OrdinalIgnoreCase)
                || scheme.Equals(ContentResolver.SchemeContent, StringComparison.OrdinalIgnoreCase));

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
            LogBuildFilePayloadFailed(ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Best-effort resolution of a human-readable resource name (including extension) from a URI.
    /// <para>
    /// For <c>content://</c> URIs the following sources are tried in order:
    /// <list type="number">
    ///   <item><description><c>_display_name</c> — already contains the extension for well-behaved providers (MediaStore, SAF, Downloads).</description></item>
    ///   <item><description><c>_data</c> — the underlying file path; its filename gives a reliable name + extension for MediaStore URIs.</description></item>
    ///   <item><description><see cref="ContentResolver.GetType"/> — maps the MIME type to an extension via <see cref="Android.Webkit.MimeTypeMap"/>.</description></item>
    ///   <item><description>Decoded <c>LastPathSegment</c> — opaque but human-readable.</description></item>
    /// </list>
    /// </para>
    /// For <c>file://</c> URIs, the real filesystem path is used directly.
    /// </summary>
    string ResolveResourceName(AndroidUri uri) =>
        ContentResolver.SchemeContent.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase)
            ? ResolveContentUriName(uri)
            : ResolveFileUriName(uri);

    string ResolveContentUriName(AndroidUri uri)
    {
        try
        {
            var (displayName, dataPath) = QueryContentColumns(uri);

            return NameWithExtension(displayName)
                ?? NameFromDataPath(dataPath)
                ?? NameFromMimeType(uri, displayName)
                ?? displayName
                ?? uri.LastPathSegment
                ?? Guid.NewGuid().ToString("N");
        }
        catch (Exception ex)
        {
            LogCouldNotResolveContentUriName(ex.Message);
            return Guid.NewGuid().ToString("N");
        }
    }

    static (string? displayName, string? dataPath) QueryContentColumns(AndroidUri uri)
    {
        string? displayName = null;
        string? dataPath = null;

        using var cursor = Application.Context.ContentResolver?.Query(
            uri,
            [Android.Provider.IOpenableColumns.DisplayName, Android.Provider.MediaStore.IMediaColumns.Data],
            selection: null,
            selectionArgs: null,
            sortOrder: null);

        if (cursor is null)
        {
            return (displayName, dataPath);
        }

        if (!cursor.MoveToFirst())
        {
            return (displayName, dataPath);
        }

        var nameIndex = cursor.GetColumnIndex(Android.Provider.IOpenableColumns.DisplayName);

        if (nameIndex >= 0)
        {
            displayName = cursor.GetString(nameIndex);
        }

        var dataIndex = cursor.GetColumnIndex(Android.Provider.MediaStore.IMediaColumns.Data);

        if (dataIndex >= 0)
        {
            dataPath = cursor.GetString(dataIndex);
        }

        return (displayName, dataPath);
    }

    static string? NameWithExtension(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName)
        && Path.GetExtension(displayName).Length > 0
            ? displayName
            : null;

    static string? NameFromDataPath(string? dataPath)
    {
        if (!string.IsNullOrEmpty(dataPath)
            && Path.GetFileName(dataPath) is { Length: > 0 } name)
        {
            return name;
        }

        return null;
    }

    // Derives an extension from the MIME type and pairs it with the display name stem.
    static string? NameFromMimeType(AndroidUri uri, string? displayName)
    {
        var mimeType = Application.Context.ContentResolver?.GetType(uri);

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return null;
        }

        var ext = Android.Webkit.MimeTypeMap.Singleton?.GetExtensionFromMimeType(mimeType);

        if (string.IsNullOrWhiteSpace(ext))
        {
            return null;
        }

        var stem = !string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(displayName)
            : Guid.NewGuid().ToString("N");

        return $"{stem}.{ext}";
    }

    static string ResolveFileUriName(AndroidUri uri)
    {
        if (uri?.Path is { Length: > 0 } filePath)
        {
            return Path.GetFileName(filePath) is { Length: > 0 } fileName
                ? fileName
                : filePath;
        }

        return Guid.NewGuid().ToString("N");
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
