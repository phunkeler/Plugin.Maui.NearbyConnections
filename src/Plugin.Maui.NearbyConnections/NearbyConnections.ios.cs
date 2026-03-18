namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    readonly ConcurrentDictionary<string, (MCNearbyServiceAdvertiserInvitationHandler Handler, CancellationTokenSource Expiry)> _pendingInvitations = new();
    readonly ConcurrentDictionary<string, IDisposable> _progressObservers = new();

    MCSession? _session;

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        var id = PeerIdManager.TrackRemotePeer(peerID);
        var device = _deviceManager.RecordDeviceFound(id, peerID.DisplayName);

        Trace.TraceInformation("{0} - Discovered nearby device: Id={1}, DisplayName={2}",
            nameof(FoundPeer),
            device.Id,
            device.DisplayName);

        Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        var id = PeerIdManager.PeerKey(peerID);

        if (_deviceManager.TryGetDevice(id, out var existingDevice)
            && existingDevice.State == NearbyDeviceState.Connected)
        {
            Trace.TraceInformation("{0} - Connected device stopped advertising (connection remains): Id={1}, DisplayName={2}",
                nameof(LostPeer),
                existingDevice.Id,
                existingDevice.DisplayName);

            return;
        }

        PeerIdManager.RemoveRemotePeer(id);
        var device = _deviceManager.RemoveDevice(id);

        Trace.TraceInformation("{0} - Device lost: Id={1}, DisplayName={2}",
            nameof(LostPeer),
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
        var id = PeerIdManager.TrackRemotePeer(peerID);

        var device = _deviceManager.SetState(id, NearbyDeviceState.ConnectionRequestedInbound)
            ?? _deviceManager.GetOrAddDevice(id, peerID.DisplayName, NearbyDeviceState.ConnectionRequestedInbound);

        var expiry = new CancellationTokenSource(Options.InvitationTimeout);
        expiry.Token.Register(() => OnInvitationExpired(device));
        _pendingInvitations.TryAdd(id, (invitationHandler, expiry));

        Trace.TraceInformation("{0} - Connection request received from: Id={1}, DisplayName={2}",
                nameof(DidReceiveInvitationFromPeer),
                device.Id,
                device.DisplayName);

        Events.OnConnectionRequested(device, TimeProvider.GetUtcNow());

        if (Options.AutoAcceptConnections
            && _pendingInvitations.TryRemove(id, out var pending))
        {
            Trace.TraceInformation("{0} - Auto-accepting connection request from: Id={1}, DisplayName={2}",
                nameof(DidReceiveInvitationFromPeer),
                device.Id,
                device.DisplayName);

            await pending.Expiry.CancelAsync();
            pending.Expiry.Dispose();

            _session ??= new MCSession(PeerIdManager.GetLocalPeerId(Options.DisplayName), identity: null!, Options.EncryptionPreference)
            {
                Delegate = new SessionDelegate(this)
            };

            pending.Handler(true, _session);
        }
    }

    #endregion Advertising

    Task PlatformDisconnectAsync(NearbyDevice device)
    {
        if (_session is not null
            && PeerIdManager.TryGetRemotePeer(device.Id, out var peerID))
        {
            using var controlData = NSData.FromArray(ControlMessage.Encode(ControlMessageType.Disconnect));
            _session.SendData(controlData, [peerID], MCSessionSendDataMode.Reliable, out var error);

            if (error is not null)
            {
                Trace.TraceWarning("{0} - Failed to send disconnect message to device: Id={1}, DisplayName={2}, Error={3}",
                    nameof(PlatformDisconnectAsync),
                    device.Id,
                    device.DisplayName,
                    error.LocalizedDescription);

                throw new InvalidOperationException($"Failed to send disconnect message to device: Id={device.Id}, DisplayName={device.DisplayName}, Error={error.LocalizedDescription}");
            }
        }

        PeerIdManager.RemoveRemotePeer(device.Id);
        var disconnectedDevice = _deviceManager.RemoveDevice(device.Id);

        if (disconnectedDevice is not null)
        {
            Events.OnDeviceDisconnected(disconnectedDevice, TimeProvider.GetUtcNow());
        }

        return Task.CompletedTask;
    }

    Task PlatformRequestConnectionAsync(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_discoverer is null || !IsDiscovering)
        {
            return Task.CompletedTask;
        }

        if (!PeerIdManager.TryGetRemotePeer(device.Id, out var peerID))
        {
            Trace.TraceWarning("{0} - No peer found for device: Id={1}, DisplayName{2}",
                nameof(PlatformRequestConnectionAsync),
                device.Id,
                device.DisplayName);

            return Task.CompletedTask;
        }

        _session ??= new MCSession(PeerIdManager.GetLocalPeerId(Options.DisplayName), identity: null!, Options.EncryptionPreference)
        {
            Delegate = new SessionDelegate(this)
        };

        _deviceManager.SetState(device.Id, NearbyDeviceState.ConnectionRequestedOutbound);
        _discoverer.InvitePeer(peerID, _session, context: null, Options.InvitationTimeout.TotalSeconds);

        return Task.CompletedTask;
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_pendingInvitations.TryRemove(device.Id, out var pending))
        {
            return Task.CompletedTask;
        }

        pending.Expiry.Cancel();
        pending.Expiry.Dispose();

        _session ??= new MCSession(PeerIdManager.GetLocalPeerId(Options.DisplayName), identity: null!, Options.EncryptionPreference)
        {
            Delegate = new SessionDelegate(this)
        };

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
        CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No active session. Ensure a connection has been established before sending data.");
        }

        if (!PeerIdManager.TryGetRemotePeer(device.Id, out var peerID))
        {
            throw new InvalidOperationException($"No peer found for device: Id={device.Id}, DisplayName={device.DisplayName}");
        }

        return SendBytesAsync(data, peerID, cancellationToken);
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

        if (!PeerIdManager.TryGetRemotePeer(device.Id, out var peerID))
        {
            throw new InvalidOperationException($"No peer found for device: Id={device.Id}, DisplayName={device.DisplayName}");
        }

        using var nsUrl = NSUrl.FromFilename(uri);
        using var transfer = new OutgoingTransfer(progress, Options.TransferInactivityTimeout);
        var resourceName = nsUrl.LastPathComponent ?? Path.GetFileName(uri);
        var sendTask = _session.SendResourceAsync(nsUrl, resourceName, peerID, out var nsProgress);

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

    Task SendBytesAsync(
        byte[] bytes,
        MCPeerID peerID,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var nsData = NSData.FromArray(bytes);
        _session!.SendData(nsData, [peerID], MCSessionSendDataMode.Reliable, out var error);

        if (error is not null)
        {
            throw new InvalidOperationException($"Failed to send bytes to '{peerID.DisplayName}': {error.LocalizedDescription}");
        }

        return Task.CompletedTask;
    }

    void OnInvitationExpired(NearbyDevice device)
    {
        if (_isDisposed || !_pendingInvitations.TryRemove(device.Id, out var expired))
        {
            return;
        }

        Trace.TraceInformation("{0} - Invitation from: Id={1}, DisplayName={2}, expired after {3} seconds",
            nameof(OnInvitationExpired),
            device.Id,
            device.DisplayName,
            Options.InvitationTimeout.TotalSeconds);

        expired.Expiry.Dispose();
        expired.Handler(false, null);

        // Reset to Discovered so the browser doesn't need to re-fire FoundPeer
        // (MPC browser still considers this peer "known" — it never fired LostPeer)
        var d = _deviceManager.SetState(device.Id, NearbyDeviceState.Discovered);

        if (d is not null)
        {
            Events.OnConnectionResponded(device, TimeProvider.GetUtcNow(), false);
            Events.OnDeviceFound(device, TimeProvider.GetUtcNow());
        }
    }

    void PlatformDispose()
    {
        foreach (var (_, pending) in _pendingInvitations)
        {
            pending.Expiry.Cancel();
            pending.Expiry.Dispose();
            pending.Handler(false, null);
        }
        _pendingInvitations.Clear();

        foreach (var (_, observer) in _progressObservers)
        {
            observer.Dispose();
        }
        _progressObservers.Clear();
        PeerIdManager.ClearRemotePeers();

        if (_session is not null)
        {
            _session.Disconnect();
            _session.Dispose();
            _session = null;
        }
    }

    #region Session Callbacks

    public void OnPeerStateChanged(MCPeerID peerID, MCSessionState state)
    {
        var id = PeerIdManager.PeerKey(peerID);

        Trace.TraceInformation("{0} - Peer state changed: Id={1}, DisplayName={2}, State={3}",
            nameof(OnPeerStateChanged),
            id,
            peerID.DisplayName,
            state);

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
                    // MPC fires NotConnected for the departing peer before removing it from
                    // ConnectedPeers, so check whether this peer was the only remaining one
                    // while it is still present in the session's list.
                    var isLastPeer = _session is not null
                        && _session.ConnectedPeers.All(p => PeerIdManager.PeerKey(p) == id);

                    PeerIdManager.RemoveRemotePeer(id);
                    var disconnectedDevice = _deviceManager.RemoveDevice(id);
                    if (disconnectedDevice is not null)
                    {
                        Events.OnDeviceDisconnected(disconnectedDevice, TimeProvider.GetUtcNow());
                    }

                    if (isLastPeer)
                    {
                        _session!.Dispose();
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
        var id = PeerIdManager.PeerKey(peerID);

        Trace.TraceInformation("{0} - Data received from peer: Id={1}, DisplayName={2}, Length={3:N0} bytes",
            nameof(OnDataReceived),
            id,
            peerID.DisplayName,
            data.Length);

        var bytes = data.ToArray();

        if (ControlMessage.TryDecode(bytes, out var controlType))
        {
            Trace.TraceInformation("{0} - Control message received from peer: Id={1}, DisplayName={2}, Type={3}",
                nameof(OnDataReceived),
                id,
                peerID.DisplayName,
                controlType);

            HandleControlMessage(controlType);
            return;
        }

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);

        if (device is null)
        {
            Trace.TraceWarning("{0} - Dropping received data from unknown peer: Id={1}, DisplayName={2}",
                nameof(OnDataReceived),
                id,
                peerID.DisplayName);

            return;
        }

        var payload = new BytesPayload(bytes);
        Events.OnDataReceived(device, payload, TimeProvider.GetUtcNow());
    }

    void HandleControlMessage(ControlMessageType type)
    {
        switch (type)
        {
            case ControlMessageType.Disconnect:
                Trace.TraceInformation("{0} - Disconnecting from session.", nameof(HandleControlMessage));
                _session?.Disconnect();
                break;
            default:
                Trace.TraceWarning("{0} - Unknown control message type: {1}",
                    nameof(HandleControlMessage),
                    type);
                break;
        }
    }

    void OnResourceStarted(string resourceName, MCPeerID fromPeer, NSProgress progress)
    {
        var id = PeerIdManager.PeerKey(fromPeer);

        Trace.TraceInformation("{0} - Started receiving resource from: Id={1}, DisplayName={2}, ResourceName={3}",
            nameof(OnResourceStarted),
            id,
            fromPeer.DisplayName,
            resourceName);

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);

        if (device is null)
        {
            Trace.TraceWarning("{0} - No peer found for device: Id={1}, DisplayName={2}",
                nameof(OnResourceStarted),
                id,
                fromPeer.DisplayName);

            return;
        }

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

    void OnResourceFinished(
        string resourceName,
        MCPeerID fromPeer,
        NSUrl? localUrl,
        NSError? error)
    {
        var id = PeerIdManager.PeerKey(fromPeer);

        Trace.TraceInformation("{0} - Finished receiving resource from: Id={1}, DisplayName={2}, ResourceName={3}, Location={4}, Error={5}",
            nameof(OnResourceFinished),
            id,
            fromPeer.DisplayName,
            resourceName,
            localUrl,
            error);


        if (_progressObservers.TryRemove(resourceName, out var observer))
        {
            observer.Dispose();
        }

        var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == id);

        if (device is null)
        {
            Trace.TraceWarning("{0} - No peer found for device: Id={1}, DisplayName={2}",
                nameof(OnResourceFinished),
                id,
                fromPeer.DisplayName);

            return;
        }

        if (error is not null)
        {
            Events.OnError("ReceiveFile", error.LocalizedDescription, TimeProvider.GetUtcNow());
            return;
        }

        if (localUrl?.Path is not string sourcePath)
        {
            Events.OnError("ReceiveFile", "Resource URL has no file path.", TimeProvider.GetUtcNow());
            return;
        }

        var destinationPath = Path.Combine(Options.ReceivedFilesDirectory, resourceName);

        try
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Trace.TraceError("{0} - Error copying received file: Source={1}, Destination={2}, Error={3}",
                nameof(OnResourceFinished),
                sourcePath,
                destinationPath,
                ex.Message);

            Events.OnError("ReceiveFile", ex.Message, TimeProvider.GetUtcNow());
            return;
        }
        finally
        {
            try
            {
                File.Delete(sourcePath);
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0} - Error deleting received file: Path={1}, Error={2}",
                    nameof(OnResourceFinished),
                    sourcePath,
                    ex.Message);
            }
        }

        var payload = new FilePayload(new FileResult(destinationPath));
        Events.OnDataReceived(device, payload, TimeProvider.GetUtcNow());
    }

    #endregion Session Callbacks

    sealed class SessionDelegate(NearbyConnectionsImplementation nearbyConnections) : NSObject, IMCSessionDelegate
    {
#pragma warning disable S1144, S1172
        public void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
            => nearbyConnections.OnPeerStateChanged(peerID, state);

        public void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            => nearbyConnections.OnDataReceived(data, peerID);

        public void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
            => nearbyConnections.OnResourceStarted(resourceName, fromPeer, progress);

        public void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
            => nearbyConnections.OnResourceFinished(resourceName, fromPeer, localUrl, error);
#pragma warning restore S1144, S1172
    }
}