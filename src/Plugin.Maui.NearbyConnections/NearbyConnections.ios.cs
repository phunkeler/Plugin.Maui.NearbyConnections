namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
    internal MyPeerIdManager MyMCPeerIDManager { get; } = new();

    readonly ConcurrentDictionary<string, MCNearbyServiceAdvertiserInvitationHandler> _pendingInvitations = new();

    MCSession? _session;

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = _deviceManager.DeviceFound(id, peerID.DisplayName);

        Trace.WriteLine($"Found peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.WriteLine($"Lost peer: Id={id}, DisplayName={peerID.DisplayName}");
        var device = _deviceManager.DeviceLost(id);
        if (device is not null)
        {
            Events.OnDeviceLost(device, TimeProvider.GetUtcNow());
        }
    }

    #endregion Discovery

    #region Advertising

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        _advertiser?.OnAdvertisingFailed();

        Events.OnError(
            "Advertising",
            error.LocalizedDescription,
            TimeProvider.GetUtcNow());
    }

    internal async void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        var device = _deviceManager.SetState(id, NearbyDeviceState.ConnectionRequestedInbound)
            ?? _deviceManager.GetOrAddDevice(id, peerID.DisplayName, NearbyDeviceState.ConnectionRequestedInbound);

        _pendingInvitations.TryAdd(id, invitationHandler);

        Trace.WriteLine($"Received invitation from peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnConnectionRequested(device, TimeProvider.GetUtcNow());

        if (Options.AutoAcceptConnections)
        {
            await PlatformRespondToConnectionAsync(device, accept: true);
        }
    }

    #endregion Advertising

    Task PlatformRequestConnectionAsync(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_discoverer is null || !IsDiscovering)
        {
            throw new InvalidOperationException("Cannot request connection: discovery is not active. Start discovery first.");
        }

        // Decode the base64 device ID back to NSData
        var peerIdData = new NSData(device.Id, NSDataBase64DecodingOptions.None);

        // Unarchive the MCPeerID
        var peerID = MyMCPeerIDManager.UnarchivePeerId(peerIdData)
            ?? throw new InvalidOperationException($"Failed to unarchive peer ID for device: {device.DisplayName}");

        // Create or get session
        if (_session is null)
        {
            var myPeerId = MyMCPeerIDManager.GetPeerId(Options.DisplayName)
                ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

            _session = new MCSession(myPeerId)
            {
                Delegate = new SessionDelegate(this)
            };
        }

        _deviceManager.SetState(device.Id, NearbyDeviceState.ConnectionRequestedOutbound);

        Trace.WriteLine($"Inviting peer: Id={device.Id}, DisplayName={peerID.DisplayName}");
        _discoverer.InvitePeer(peerID, _session, context: null, Options.InvitationTimeout);

        return Task.CompletedTask;
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_pendingInvitations.TryRemove(device.Id, out var invitationHandler))
        {
            throw new InvalidOperationException(
                $"No pending invitation found for device: {device.DisplayName}");
        }

        // Create or reuse session (same pattern as PlatformRequestConnectionAsync)
        if (_session is null)
        {
            var myPeerId = MyMCPeerIDManager.GetPeerId(Options.DisplayName)
                ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

            _session = new MCSession(myPeerId)
            {
                Delegate = new SessionDelegate(this)
            };
        }

        invitationHandler(accept, accept ? _session : null);

        if (!accept)
        {
            _deviceManager.SetState(device.Id, NearbyDeviceState.Discovered);
        }

        return Task.CompletedTask;
    }

    Task PlatformSendAsync(
        NearbyDevice device,
        byte[] data,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No active session. Ensure a connection has been established before sending data.");
        }

        var peerIdData = new NSData(device.Id, NSDataBase64DecodingOptions.None);
        var peerID = MyMCPeerIDManager.UnarchivePeerId(peerIdData)
            ?? throw new InvalidOperationException($"Failed to unarchive peer ID for device: {device.DisplayName}");

        return SendBytesAsync(data, peerID, progress, cancellationToken);
    }

    Task SendBytesAsync(byte[] bytes, MCPeerID peerID, IProgress<NearbyTransferProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var nsData = NSData.FromArray(bytes);
        _session!.SendData(nsData, [peerID], MCSessionSendDataMode.Reliable, out var error);

        if (error is not null)
        {
            throw new InvalidOperationException($"Failed to send bytes to '{peerID.DisplayName}': {error.LocalizedDescription}");
        }

        progress?.Report(new NearbyTransferProgress(
            payloadId: 0,
            bytesTransferred: bytes.Length,
            totalBytes: bytes.Length,
            NearbyTransferStatus.Success));

        return Task.CompletedTask;
    }

    Task PlatformSendAsync(
        NearbyDevice device,
        Func<Task<Stream>> streamFactory,
        string streamName,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No active session. Ensure a connection has been established before sending data.");
        }

        var peerIdData = new NSData(device.Id, NSDataBase64DecodingOptions.None);
        var peerID = MyMCPeerIDManager.UnarchivePeerId(peerIdData)
            ?? throw new InvalidOperationException($"Failed to unarchive peer ID for device: {device.DisplayName}");

        return SendStreamAsync(streamFactory, streamName, peerID, cancellationToken);
    }

    async Task SendFileAsync(FilePayload payload, MCPeerID peerID, IProgress<NearbyTransferProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resourceUrl = NSUrl.FromFilename(payload.FilePath);
        var resourceName = Path.GetFileName(payload.FilePath);

        if (progress is null)
        {
            await _session!.SendResourceAsync(resourceUrl, resourceName, peerID);
            return;
        }

        // Start the transfer — the binding returns the NSProgress via out parameter.
        // We attach KVO after the call returns the handle, then await the task.
        var task = _session!.SendResourceAsync(resourceUrl, resourceName, peerID, out var nsProgress);

        // KVO on FractionCompleted to bridge NSProgress → IProgress<NearbyTransferProgress>
        using var observation = nsProgress?.AddObserver(
            "fractionCompleted",
            NSKeyValueObservingOptions.New,
            _ =>
            {
                if (nsProgress is null) return;
                var transferred = (long)(nsProgress.FractionCompleted * nsProgress.TotalUnitCount);
                progress.Report(new NearbyTransferProgress(
                    payloadId: 0,
                    bytesTransferred: transferred,
                    totalBytes: nsProgress.TotalUnitCount,
                    NearbyTransferStatus.InProgress));
            });

        using var registration = cancellationToken.Register(() => nsProgress?.Cancel());

        try
        {
            await task;

            progress.Report(new NearbyTransferProgress(
                payloadId: 0,
                bytesTransferred: nsProgress?.TotalUnitCount ?? 0,
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Success));
        }
        catch (OperationCanceledException)
        {
            progress.Report(new NearbyTransferProgress(
                payloadId: 0,
                bytesTransferred: 0,
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Canceled));
            throw;
        }
        catch
        {
            progress.Report(new NearbyTransferProgress(
                payloadId: 0,
                bytesTransferred: 0,
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Failure));
            throw;
        }
    }

    Task SendStreamAsync(
        Func<Task<Stream>> streamFactory,
        string streamName,
        MCPeerID peerID,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var name = string.IsNullOrWhiteSpace(streamName)
            ? Guid.NewGuid().ToString("N")
            : streamName;

        var outputStream = _session!.StartStream(name, peerID, out var error);

        if (error is not null || outputStream is null)
        {
            throw new InvalidOperationException(
                $"Failed to start stream '{name}' to '{peerID.DisplayName}': {error?.LocalizedDescription ?? "unknown error"}");
        }

        return Task.Run(async () =>
        {
            outputStream.Open();
            try
            {
                using var source = await streamFactory();
                var buffer = new byte[16 * 1024];
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    outputStream.Write(buffer, (nuint)read);
                }
            }
            finally
            {
                outputStream.Close();
                outputStream.Dispose();
            }
        }, cancellationToken);
    }

    #region Session Callbacks

    internal void OnPeerStateChanged(MCPeerID peerID, MCSessionState state)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.WriteLine($"Peer state changed: Id={id}, DisplayName={peerID.DisplayName}, State={state}");

        switch (state)
        {
            case MCSessionState.Connected:
                var connectedDevice = _deviceManager.SetState(id, NearbyDeviceState.Connected);
                if (connectedDevice is not null)
                {
                    Events.OnConnectionResponded(connectedDevice, TimeProvider.GetUtcNow(), true);
                }
                break;
            case MCSessionState.NotConnected:
                var disconnectedDevice = _deviceManager.DeviceDisconnected(id);
                if (disconnectedDevice is not null)
                {
                    Events.OnDeviceDisconnected(disconnectedDevice, TimeProvider.GetUtcNow());
                }
                break;
            case MCSessionState.Connecting:
                // Connection in progress - no event needed
                break;
        }
    }

    void OnDataReceived(NSData data, MCPeerID peerID)
    {
        using var archived = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.WriteLine($"Data received from peer: DisplayName={peerID.DisplayName}, Length={data.Length}");

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);
        if (device is null)
        {
            Trace.WriteLine($"Received data from unknown peer: {peerID.DisplayName}");
            return;
        }

        var payload = new BytesPayload(data.ToArray());
        Events.OnDataReceived(device, payload, TimeProvider.GetUtcNow());
    }

    void OnResourceStarted(string resourceName, MCPeerID fromPeer, NSProgress progress)
    {
        using var archived = MyMCPeerIDManager.ArchivePeerId(fromPeer);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.WriteLine($"Started receiving resource: {resourceName} from {fromPeer.DisplayName}");

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);
        if (device is null)
        {
            return;
        }

        // KVO on the incoming NSProgress to raise IncomingTransferProgress events
        progress.AddObserver(
            "fractionCompleted",
            NSKeyValueObservingOptions.New,
            _ =>
            {
                var transferred = (long)(progress.FractionCompleted * progress.TotalUnitCount);
                Events.OnIncomingTransferProgress(
                    device,
                    new NearbyTransferProgress(
                        payloadId: 0,
                        bytesTransferred: transferred,
                        totalBytes: progress.TotalUnitCount,
                        NearbyTransferStatus.InProgress),
                    TimeProvider.GetUtcNow());
            });
    }

    void OnResourceFinished(string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
    {
        using var archived = MyMCPeerIDManager.ArchivePeerId(fromPeer);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.WriteLine($"Finished receiving resource: {resourceName} from {fromPeer.DisplayName}, Error: {error?.LocalizedDescription ?? "None"}");

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);
        if (device is null)
        {
            return;
        }

        if (error is not null || localUrl is null)
        {
            Events.OnError("ReceiveFile", error?.LocalizedDescription ?? "Unknown error receiving resource", TimeProvider.GetUtcNow());
            return;
        }

        var payload = new FilePayload(localUrl.Path!);
        Events.OnDataReceived(device, payload, TimeProvider.GetUtcNow());
    }

    void OnStreamReceived(NSInputStream stream, string streamName, MCPeerID fromPeer)
    {
        using var archived = MyMCPeerIDManager.ArchivePeerId(fromPeer);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.WriteLine($"Received stream: {streamName} from {fromPeer.DisplayName}");

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);
        if (device is null)
        {
            return;
        }

        stream.Open();
        // Wrap the NSInputStream in a MemoryStream by draining it on a background thread,
        // then raise DataReceived once all bytes are available.
        _ = Task.Run(() =>
        {
            try
            {
                var buffer = new byte[16 * 1024];
                using var ms = new MemoryStream();

                while (stream.HasBytesAvailable())
                {
                    var read = (int)stream.Read(buffer, (nuint)buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, read);
                }

                var captured = ms.ToArray();
                var payload = new StreamPayload(() => new MemoryStream(captured), streamName);
                Events.OnDataReceived(device, payload, TimeProvider.GetUtcNow());
            }
            finally
            {
                stream.Close();
                stream.Dispose();
            }
        });
    }

    #endregion Session Callbacks

    sealed class SessionDelegate(NearbyConnectionsImplementation nearbyConnections) : NSObject, IMCSessionDelegate
    {
        public void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
            => nearbyConnections.OnPeerStateChanged(peerID, state);

        public void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            => nearbyConnections.OnDataReceived(data, peerID);

        public void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
            => nearbyConnections.OnResourceStarted(resourceName, fromPeer, progress);

        public void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
            => nearbyConnections.OnResourceFinished(resourceName, fromPeer, localUrl, error);

        public void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerID)
            => nearbyConnections.OnStreamReceived(stream, streamName, peerID);
    }
}