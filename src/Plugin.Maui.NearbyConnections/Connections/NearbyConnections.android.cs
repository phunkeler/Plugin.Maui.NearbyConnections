using Android.Content;
using AndroidUri = Android.Net.Uri;
using Path = System.IO.Path;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    IConnectionsClient? _advertiseClient;
    IConnectionsClient? _discoverClient;

    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, OutgoingTransfer> _outgoingTransfers = [];
    readonly ConcurrentDictionary<string, bool> _inboundEndpoints = [];

    public bool IsAdvertising
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnAdvertisingStateChanged(value, TimeProvider.GetUtcNow());
            }
        }
    }

    public bool IsDiscovering
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnDiscoveringStateChanged(value, TimeProvider.GetUtcNow());
            }
        }
    }

    #region Advertising

    async Task PlatformStartAdvertisingAsync()
    {
        _advertiseClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        await _advertiseClient.StartAdvertisingAsync(
            Options.DisplayName,
            Options.ServiceId,
            new AdvertiseCallback(OnConnectionInitiated, OnConnectionResult, OnDisconnected),
            new AdvertisingOptions.Builder()
                .SetStrategy(Options.Strategy)
                .SetConnectionType(Options.ConnectionType)
                .SetLowPower(Options.UseLowPower)
                .Build());

        IsAdvertising = true;
    }

    void PlatformStopAdvertising()
    {
        _advertiseClient?.StopAdvertising();
        _advertiseClient?.Dispose();
        _advertiseClient = null;
        IsAdvertising = false;
    }

    /// <summary>
    /// "A basic encrypted channel has been created between you and the endpoint.
    /// Both sides are now asked if they wish to accept or reject the connection before any data can be sent over this channel."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectioninitiated-string-endpointid,-connectioninfo-connectioninfo">developers.google.com</see>
    /// </summary>
    public async void OnConnectionInitiated(string endpointId, ConnectionInfo connectionInfo)
    {
        var state = connectionInfo.IsIncomingConnection
            ? NearbyDeviceState.ConnectionRequestedInbound
            : NearbyDeviceState.ConnectionRequestedOutbound;

        var device = _deviceManager.SetState(endpointId, state)
            ?? _deviceManager.GetOrAddDevice(endpointId, connectionInfo.EndpointName, state);

        if (connectionInfo.IsIncomingConnection)
        {
            _inboundEndpoints.TryAdd(endpointId, true);
            LogConnectionRequestReceived(device.Id, device.DisplayName);

            OnConnectionRequested(device, TimeProvider.GetUtcNow());

            if (Options.AutoAcceptConnections)
            {
                LogAutoAcceptingConnection(device.Id, device.DisplayName);
                await PlatformRespondToConnectionAsync(device, accept: true);
            }
        }
        else
        {
            // Skip this extra step - we, as the discoverer, initiated the request
            await PlatformRespondToConnectionAsync(device, accept: true);
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
        LogConnectionResult(endpointId, resolution.Status.StatusCode, resolution.Status.StatusMessage ?? string.Empty, resolution.Status.IsSuccess);

        if (resolution.Status.IsSuccess)
        {
            _inboundEndpoints.TryRemove(endpointId, out _);
            var device = _deviceManager.SetState(endpointId, NearbyDeviceState.Connected);

            if (device is not null)
            {
                OnConnectionResponded(device, TimeProvider.GetUtcNow(), true);
            }
        }
        else
        {
            NearbyDevice? device;

            if (_inboundEndpoints.TryRemove(endpointId, out _))
                device = _deviceManager.RemoveDevice(endpointId);
            else
                device = _deviceManager.SetState(endpointId, NearbyDeviceState.Discovered);

            if (device is not null)
            {
                OnConnectionResponded(device, TimeProvider.GetUtcNow(), false);
            }
        }
    }

    /// <summary>
    /// "Called when a remote endpoint is disconnected or has become unreachable."
    /// -- <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-ondisconnected-string-endpointid">developers.google.com</see>
    /// </summary>
    public void OnDisconnected(string endpointId)
    {
        LogDeviceDisconnected(endpointId);

        var device = _deviceManager.RemoveDevice(endpointId);

        if (device is not null)
        {
            OnDeviceDisconnected(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Advertising

    #region Discovery

    async Task PlatformStartDiscoveringAsync()
    {
        _discoverClient ??= NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        await _discoverClient.StartDiscoveryAsync(
            Options.ServiceId,
            new DiscoveryCallback(OnEndpointFound, OnEndpointLost),
            new DiscoveryOptions.Builder()
                .SetStrategy(Options.Strategy)
                .SetLowPower(Options.UseLowPower)
                .Build());

        IsDiscovering = true;
    }

    void PlatformStopDiscovering()
    {
        _discoverClient?.StopDiscovery();
        _discoverClient?.Dispose();
        _discoverClient = null;
        IsDiscovering = false;
    }

    public void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        var device = _deviceManager.RecordDeviceFound(endpointId, info.EndpointName);

        LogDeviceFound(device.Id, device.DisplayName);

        OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    public void OnEndpointLost(string endpointId)
    {
        if (_deviceManager.TryGetDevice(endpointId, out var existingDevice)
            && existingDevice.State == NearbyDeviceState.Connected)
        {
            LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
            return;
        }

        var device = _deviceManager.RemoveDevice(endpointId);

        LogDeviceLost(endpointId, device?.DisplayName);

        if (device is not null)
        {
            OnDeviceLost(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Discovery

    void OnPayloadReceived(string endpointId, Payload payload)
    {
        LogPayloadReceived(endpointId, payload.Id, payload.PayloadType);

        _incomingPayloads.TryAdd(payload.Id, (endpointId, payload));
    }

    async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        LogPayloadTransferUpdate(endpointId, update.PayloadId, update.TransferStatus, update.TotalBytes, update.BytesTransferred);

        var status = ToNearbyTransferStatus(update.TransferStatus);

        if (_outgoingTransfers.TryGetValue(update.PayloadId, out var outgoingTransfer))
        {
            outgoingTransfer.OnUpdate(new NearbyTransferProgress(
                payloadId: update.PayloadId,
                bytesTransferred: update.BytesTransferred,
                totalBytes: update.TotalBytes,
                status));

            return;
        }

        if (!_deviceManager.TryGetDevice(endpointId, out var device))
        {
            return;
        }

        OnIncomingTransferProgress(
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

        NearbyPayload? nearbyPayload = null;

        if (entry.Payload.PayloadType == Payload.Type.File)
        {
            nearbyPayload = await CopyFilePayloadAsync(entry.Payload, Options.ReceivedFilesDirectory, CancellationToken.None);
        }
        else
        {
            nearbyPayload = entry.Payload.AsBytes() is { } bytes
                ? new BytesPayload(bytes)
                : null;
        }

        if (nearbyPayload is not null)
        {
            OnDataReceived(device, nearbyPayload, TimeProvider.GetUtcNow());
        }
        else
        {
            OnError(
                operation: nameof(OnIncomingPayloadSuccess),
                errorMessage: $"Failed to process incoming payload with ID {payloadId}.",
                timeStamp: TimeProvider.GetUtcNow(),
                device);
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

    Task PlatformDisconnectAsync(NearbyDevice device)
    {
        LogDisconnecting(device.Id, device.DisplayName);

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        client.DisconnectFromEndpoint(device.Id);

        var disconnectedDevice = _deviceManager.RemoveDevice(device.Id);
        if (disconnectedDevice is not null)
        {
            OnDeviceDisconnected(disconnectedDevice, TimeProvider.GetUtcNow());
        }

        return Task.CompletedTask;
    }

    Task PlatformRequestConnectionAsync(NearbyDevice device)
    {
        _deviceManager.SetState(device.Id, NearbyDeviceState.ConnectionRequestedOutbound);

        return NearbyClass
            .GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext)
            .RequestConnectionAsync(
                Options.DisplayName,
                device.Id,
                new AdvertiseCallback(
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

    static Task PlatformSendAsync(
        NearbyDevice device,
        byte[] data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var payload = Payload.FromBytes(data);
        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);

        return client.SendPayloadAsync(device.Id, payload);
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

    async Task PlatformSendAsync(
        NearbyDevice device,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var androidUri = TryCreateUri(uri);

        if (androidUri is null)
        {
            LogInvalidFileUri(uri);
            return;
        }

        var filePayload = BuildFilePayload(androidUri);

        if (filePayload is null)
        {
            return;
        }

        var client = NearbyClass.GetConnectionsClient(Platform.CurrentActivity ?? Platform.AppContext);
        var transfer = new OutgoingTransfer(progress, Options.TransferInactivityTimeout);

        _outgoingTransfers.TryAdd(filePayload.Id, transfer);

        try
        {
            await client.SendPayloadAsync(device.Id, filePayload);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(() => _ = client.CancelPayloadAsync(filePayload.Id));
            await transfer.Completion.WaitAsync(linkedCts.Token);
        }
        finally
        {
            _outgoingTransfers.TryRemove(filePayload.Id, out _);
            transfer.Dispose();
            filePayload.Dispose();
        }
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
        _inboundEndpoints.Clear();
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
        Func<string, PayloadTransferUpdate, Task> onPayloadTransferUpdate) : PayloadCallback
    {
        public override void OnPayloadReceived(string p0, Payload p1)
            => onPayloadReceived(p0, p1);

        public override async void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            => await onPayloadTransferUpdate(p0, p1);
    }
}
