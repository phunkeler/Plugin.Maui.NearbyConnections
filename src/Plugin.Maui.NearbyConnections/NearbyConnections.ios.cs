namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    readonly ConcurrentDictionary<string, (MCNearbyServiceAdvertiserInvitationHandler Handler, CancellationTokenSource Expiry)> _pendingInvitations = new();
    readonly ConcurrentDictionary<string, IDisposable> _progressObservers = new();

    MCSession? _session;

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        using var data = PeerIdManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
        var device = _deviceManager.RecordDeviceFound(id, peerID.DisplayName);

        Trace.TraceInformation("Found peer: Id={0}, DisplayName={1}", id, peerID.DisplayName);
        Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        using var data = PeerIdManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        if (_deviceManager.TryGetDevice(id, out var existingDevice)
            && existingDevice.State == NearbyDeviceState.Connected)
        {
            Trace.TraceInformation("Connected device stopped advertising (connection remains): Id={0}, DisplayName={1}",
                existingDevice.Id,
                existingDevice.DisplayName);

            return;
        }

        var device = _deviceManager.RemoveDevice(id);
        Trace.TraceInformation("Device lost: Id={0}, DisplayName={1}",
            id,
            device?.DisplayName);

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

    internal async Task DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        using var data = PeerIdManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        var device = _deviceManager.SetState(id, NearbyDeviceState.ConnectionRequestedInbound)
            ?? _deviceManager.GetOrAddDevice(id, peerID.DisplayName, NearbyDeviceState.ConnectionRequestedInbound);

        var expiry = new CancellationTokenSource(TimeSpan.FromSeconds(Options.InvitationTimeout));
        expiry.Token.Register(() => OnInvitationExpired(id));
        _pendingInvitations.TryAdd(id, (invitationHandler, expiry));

        Trace.TraceInformation("Received invitation from peer: Id={0}, DisplayName={1}", id, peerID.DisplayName);
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
        var peerID = PeerIdManager.UnarchivePeerId(peerIdData)
            ?? throw new InvalidOperationException($"Failed to unarchive peer ID for device: {device.DisplayName}");

        // Create or get session
        if (_session is null)
        {
            var myPeerId = PeerIdManager.GetLocalPeerId(Options.DisplayName)
                ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

            _session = new MCSession(myPeerId)
            {
                Delegate = new SessionDelegate(this)
            };
        }

        _deviceManager.SetState(device.Id, NearbyDeviceState.ConnectionRequestedOutbound);

        Trace.TraceInformation("Inviting peer: Id={0}, DisplayName={1}", device.Id, peerID.DisplayName);
        _discoverer.InvitePeer(peerID, _session, context: null, Options.InvitationTimeout);

        return Task.CompletedTask;
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_pendingInvitations.TryRemove(device.Id, out var pending))
        {
            throw new InvalidOperationException(
                $"No pending invitation found for device: {device.DisplayName}");
        }

        pending.Expiry.Cancel();
        pending.Expiry.Dispose();

        // Create or reuse session (same pattern as PlatformRequestConnectionAsync)
        if (_session is null)
        {
            var myPeerId = PeerIdManager.GetLocalPeerId(Options.DisplayName)
                ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

            _session = new MCSession(myPeerId)
            {
                Delegate = new SessionDelegate(this)
            };
        }

        pending.Handler(accept, accept ? _session : null);

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
        var peerID = PeerIdManager.UnarchivePeerId(peerIdData)
            ?? throw new InvalidOperationException($"Failed to unarchive peer ID for device: {device.DisplayName}");

        return SendBytesAsync(data, peerID, progress, cancellationToken);
    }

    async Task PlatformSendAsync(
        NearbyDevice device,
        string uri,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No active session. Ensure a connection has been established before sending data.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var peerIdData = new NSData(device.Id, NSDataBase64DecodingOptions.None);
        var peerID = PeerIdManager.UnarchivePeerId(peerIdData)
            ?? throw new InvalidOperationException($"Failed to unarchive peer ID for device: {device.DisplayName}");

        using var nsUrl = NSUrl.FromFilename(uri);
        var resourceName = nsUrl.LastPathComponent ?? Path.GetFileName(uri);

        var sendTask = _session.SendResourceAsync(nsUrl, resourceName, peerID, out var nsProgress);

        using var transfer = new OutgoingTransfer(progress, Options.TransferInactivityTimeout);

        IDisposable? observer = null;
        if (nsProgress is not null)
        {
            observer = nsProgress.AddObserver(
                "fractionCompleted",
                NSKeyValueObservingOptions.New,
                _ =>
                {
                    var transferred = (long)(nsProgress.FractionCompleted * nsProgress.TotalUnitCount);
                    transfer.OnUpdate(new NearbyTransferProgress(
                        payloadId: 0,
                        bytesTransferred: transferred,
                        totalBytes: nsProgress.TotalUnitCount,
                        NearbyTransferStatus.InProgress));
                });
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(() => nsProgress?.Cancel());
            await sendTask;

            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: 0,
                bytesTransferred: nsProgress?.TotalUnitCount ?? 0,
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Success));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: 0,
                bytesTransferred: (long)((nsProgress?.FractionCompleted ?? 0) * (nsProgress?.TotalUnitCount ?? 0)),
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Canceled));
            throw;
        }
        catch (OperationCanceledException) when (transfer.InactivityToken.IsCancellationRequested)
        {
            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: 0,
                bytesTransferred: (long)((nsProgress?.FractionCompleted ?? 0) * (nsProgress?.TotalUnitCount ?? 0)),
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Failure));
            throw new TimeoutException(
                $"Transfer stalled: no progress received for {Options.TransferInactivityTimeout}.");
        }
        catch
        {
            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: 0,
                bytesTransferred: (long)((nsProgress?.FractionCompleted ?? 0) * (nsProgress?.TotalUnitCount ?? 0)),
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Failure));
            throw;
        }
        finally
        {
            observer?.Dispose();
        }
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

    void OnInvitationExpired(string id)
    {
        if (!_pendingInvitations.TryRemove(id, out var expired))
        {
            return;
        }

        Trace.TraceInformation("Invitation expired: Id={0}", id);

        expired.Expiry.Dispose();
        expired.Handler(false, null);

        // Reset to Discovered so the browser doesn't need to re-fire FoundPeer
        // (MPC browser still considers this peer "known" — it never fired LostPeer)
        var device = _deviceManager.SetState(id, NearbyDeviceState.Discovered);
        if (device is not null)
        {
            Events.OnConnectionResponded(device, TimeProvider.GetUtcNow(), false);
            Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
        }
    }

    #region Session Callbacks

    public void OnPeerStateChanged(MCPeerID peerID, MCSessionState state)
    {
        using var data = PeerIdManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.TraceInformation("Peer state changed: Id={0}, DisplayName={1}, State={2}", id, peerID.DisplayName, state);

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
                if (_pendingInvitations.TryRemove(id, out var stale))
                {
                    // Advertiser side: a pending inbound invite went NotConnected
                    // (remote disappeared mid-handshake before we responded).
                    stale.Expiry.Cancel();
                    stale.Expiry.Dispose();
                    stale.Handler(false, null);

                    // Reset to Discovered — LostPeer will clean up if the peer truly disappeared.
                    // If it's still advertising, the UI can reconnect without waiting for FoundPeer.
                    var pendingDevice = _deviceManager.SetState(id, NearbyDeviceState.Discovered);
                    if (pendingDevice is not null)
                    {
                        Events.OnConnectionResponded(pendingDevice, TimeProvider.GetUtcNow(), false);
                        Events.OnDeviceFound(pendingDevice, TimeProvider.GetUtcNow());
                    }
                }
                else if (_deviceManager.TryGetDevice(id, out var outboundDevice)
                    && outboundDevice.State == NearbyDeviceState.ConnectionRequestedOutbound)
                {
                    // Discoverer side: our outbound invite was rejected or expired.
                    // Reset to Discovered so the UI can reconnect immediately.
                    _deviceManager.SetState(id, NearbyDeviceState.Discovered);
                    Events.OnConnectionResponded(outboundDevice, TimeProvider.GetUtcNow(), false);
                    Events.OnDeviceFound(outboundDevice, TimeProvider.GetUtcNow());
                }
                else
                {
                    var disconnectedDevice = _deviceManager.RemoveDevice(id);
                    if (disconnectedDevice is not null)
                    {
                        Events.OnDeviceDisconnected(disconnectedDevice, TimeProvider.GetUtcNow());
                    }

                    if (_session is not null && _session.ConnectedPeers.Length == 0)
                    {
                        _session.Dispose();
                        _session = null;
                    }
                }
                break;
            case MCSessionState.Connecting:
                // Connection in progress - no event needed
                break;
        }
    }

    void OnDataReceived(NSData data, MCPeerID peerID)
    {
        using var archived = PeerIdManager.ArchivePeerId(peerID);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.TraceInformation("Data received from peer: DisplayName={0}, Length={1}", peerID.DisplayName, data.Length);

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);
        if (device is null)
        {
            Trace.TraceInformation("Received data from unknown peer: {0}", peerID.DisplayName);
            return;
        }

        var payload = new BytesPayload(data.ToArray());
        Events.OnDataReceived(device, payload, TimeProvider.GetUtcNow());
    }

    void OnResourceStarted(string resourceName, MCPeerID fromPeer, NSProgress progress)
    {
        using var archived = PeerIdManager.ArchivePeerId(fromPeer);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.TraceInformation("Started receiving resource: {0} from {1}", resourceName, fromPeer.DisplayName);

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);
        if (device is null)
        {
            return;
        }

        // KVO on the incoming NSProgress to raise IncomingTransferProgress events
        var observer = progress.AddObserver(
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
        _progressObservers[resourceName] = observer;
    }

    void OnResourceFinished(string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
    {
        using var archived = PeerIdManager.ArchivePeerId(fromPeer);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.TraceInformation("Finished receiving resource: {0} from {1}, Error: {2}", resourceName, fromPeer.DisplayName, error?.LocalizedDescription ?? "None");

        if (_progressObservers.TryRemove(resourceName, out var observer))
        {
            observer.Dispose();
        }

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

        var sourcePath = localUrl.Path!;
        var destinationPath = Path.Combine(Options.ReceivedFilesDirectory, resourceName);

        try
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(sourcePath);
        }

        var payload = new FilePayload(new FileResult(destinationPath));
        Events.OnDataReceived(device, payload, TimeProvider.GetUtcNow());
    }

    void OnStreamReceived(NSInputStream stream, string streamName, MCPeerID fromPeer)
    {
        using var archived = PeerIdManager.ArchivePeerId(fromPeer);
        var id = archived.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        Trace.TraceInformation("Received stream: {0} from {1}", streamName, fromPeer.DisplayName);

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

#pragma warning disable S1144, S1172
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
#pragma warning restore S1144, S1172
}