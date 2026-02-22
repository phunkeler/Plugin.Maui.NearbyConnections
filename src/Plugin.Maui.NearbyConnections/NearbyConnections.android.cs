using Android.Content;
using AndroidUri = Android.Net.Uri;
using Path = System.IO.Path;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    const uint MetadataSignature = 0x504D4E43; // PMNC (Plugin.Maui.NearbyConnections)

    readonly ConcurrentDictionary<long, (string EndpointId, Payload Payload)> _incomingPayloads = [];
    readonly ConcurrentDictionary<long, IncomingStreamTransfer> _incomingStreams = [];
    readonly ConcurrentDictionary<long, OutgoingStreamTransfer> _outgoingStreams = [];
    readonly ConcurrentDictionary<long, OutgoingTransfer> _outgoingTransfers = [];

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

        _incomingPayloads.TryAdd(payload.Id, (endpointId, payload));
    }

    async Task OnPayloadTransferUpdate(string endpointId, PayloadTransferUpdate update)
    {
        Trace.TraceInformation($"Payload transfer update: EndpointId={endpointId}, PayloadId={update.PayloadId}, TransferStatus={update.TransferStatus}, TotalBytes={update.TotalBytes}, BytesTransferred={update.BytesTransferred}");

        var status = ToNearbyTransferStatus(update.TransferStatus);

        if (_outgoingTransfers.TryGetValue(update.PayloadId, out var outgoingTransfer))
        {
            outgoingTransfer.OnUpdate(new NearbyTransferProgress(
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
        using var payload = Payload.FromBytes(data);

        await client.SendPayloadAsync(device.Id, payload);

        // Bytes payloads complete synchronously on enqueue; report success immediately
        progress?.Report(new NearbyTransferProgress(
            payloadId: payload.Id,
            bytesTransferred: data.Length,
            totalBytes: data.Length,
            NearbyTransferStatus.Success));
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

    static Payload? BuildFilePayload(AndroidUri uri)
    {
        try
        {
            var parcelFileDescriptor = Application.Context.ContentResolver?.OpenFileDescriptor(uri, "r");
            var payload = parcelFileDescriptor is not null
                ? Payload.FromFile(parcelFileDescriptor)
                : null;
            var fileName = ResolveResourceName(uri);

            payload?.SetFileName(fileName);

            return payload;

        }
        catch (Exception ex)
        {
            Trace.TraceError($"{nameof(BuildFilePayload)}: Error building file payload. Message: {ex.Message}");
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
            Trace.TraceWarning($"{nameof(PlatformSendAsync)}: Cannot send file: '{uri}' is not a valid URI. Only 'file://' and 'content://' schemes are supported.");
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
    /// Holds the drain task and optional name for an in-flight incoming stream payload.
    /// </summary>
    /// <param name="DrainTask"></param>
    /// <param name="Name"></param>
    sealed record IncomingStreamTransfer(Task<StreamPayload?>? DrainTask = null, string Name = "");

    class OutgoingTransfer(
        IProgress<NearbyTransferProgress>? progress,
        TimeSpan inactivityTimeout) : IDisposable
    {
        readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource _inactivityCts = new(inactivityTimeout);

        /// <summary>
        /// Awaitable task that completes when the transfer reaches a terminal state.
        /// </summary>
        public Task Completion => _tcs.Task;

        /// <summary>
        /// Cancelled when no transfer updates have been received within the configured inactivity timeout.
        /// Reset on every call to <see cref="OnUpdate"/>.
        /// </summary>
        public CancellationToken InactivityToken => _inactivityCts.Token;

        public void OnUpdate(NearbyTransferProgress transferProgress)
        {
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _inactivityCts.Dispose();
        }
    }

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
    static string ResolveResourceName(AndroidUri uri) =>
        ContentResolver.SchemeContent.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase)
            ? ResolveContentUriName(uri)
            : ResolveFileUriName(uri);

    static string ResolveContentUriName(AndroidUri uri)
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
            Trace.TraceWarning($"Could not resolve display name from content URI: {ex.Message}");
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

    // Returns displayName only when it already carries an extension.
    static string? NameWithExtension(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName)
        && Path.GetExtension(displayName).Length > 0
            ? displayName
            : null;

    // Returns the filename from the _data column (real filesystem path).
    static string? NameFromDataPath(string? dataPath) =>
        !string.IsNullOrEmpty(dataPath)
            ? Path.GetFileName(dataPath) is { Length: > 0 } n ? n : null
            : null;

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

    static Payload BuildFileNamePayload(long filePayloadId, string fileName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(fileName);
        var metadata = new byte[12 + nameBytes.Length];

        BinaryPrimitives.WriteUInt32LittleEndian(metadata, MetadataSignature);
        BinaryPrimitives.WriteInt64LittleEndian(metadata.AsSpan(4), filePayloadId);
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