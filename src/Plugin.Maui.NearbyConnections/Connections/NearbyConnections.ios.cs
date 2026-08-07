using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    static long s_nextPayloadId;

    readonly ConcurrentDictionary<string, IDisposable> _progressObservers = [];
    readonly Lock _sessionLock = new();

    MCNearbyServiceAdvertiser? _mcAdvertiser;
    MCNearbyServiceBrowser? _mcBrowser;
    MCSession? _session;

    #region Advertising

    Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var myPeerId = LocalPeerIdentityStore.GetLocalPeerId(Options.DisplayName);

        _mcAdvertiser = new MCNearbyServiceAdvertiser(
            myPeerID: myPeerId,
            info: null,
            serviceType: Options.ServiceId)
        {
            Delegate = new AdvertiserDelegate(this)
        };

        _mcAdvertiser.StartAdvertisingPeer();

        return Task.CompletedTask;
    }

    void PlatformStopAdvertising()
    {
        _mcAdvertiser?.StopAdvertisingPeer();
        _mcAdvertiser?.Dispose();
        _mcAdvertiser = null;
    }

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        LogDidNotStartAdvertising(error.LocalizedDescription);

        if (!_advertiseChannel.Writer.TryComplete(new NearbyAdvertisingException(error.LocalizedDescription)))
        {
            LogStartAdvertisingFaultDropped();
        }
    }

    internal void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
    {
        LogDidNotStartBrowsing(error.LocalizedDescription);

        if (!_discoverChannel.Writer.TryComplete(new NearbyDiscoveryException(error.LocalizedDescription)))
        {
            LogStartDiscoveringFaultDropped();
        }
    }

    internal void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        try
        {
            var device = RemotePeers.TrackRemotePeer(PeerKeyProvider, peerID, _logger);
            var id = device.Id;

            LogConnectionRequestReceived(device.Id, device.DisplayName);

            var request = new NearbyConnectionRequest(
                device,
                acceptFactory: async ct =>
                {
                    MCSession session;
                    lock (_sessionLock)
                    {
                        _session ??= new MCSession(
                            LocalPeerIdentityStore.GetLocalPeerId(Options.DisplayName),
                            identity: null!,
                            Options.ToPlatformEncryptionPreference())
                        {
                            Delegate = new SessionDelegate(this)
                        };
                        session = _session;
                    }

                    // Register the TCS before handing the session to MPC - OnPeerStateChanged(Connected)
                    // can fire on another thread as soon as invitationHandler is called, and it only
                    // resolves a TCS that is already present in _connectionTcs.
                    var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _connectionTcs[id] = (tcs, ct);

                    invitationHandler(true, session);

                    try
                    {
                        return await tcs.Task.WaitAsync(ct);
                    }
                    catch
                    {
                        _connectionTcs.TryRemove(id, out _);
                        invitationHandler(false, null);
                        throw;
                    }
                },
                rejectFactory: ct =>
                {
                    invitationHandler(false, null);
                    RemotePeers.RemoveRemotePeer(id, _logger);
                    return Task.CompletedTask;
                });

            WriteConnectionRequest(request);
        }
        catch (Exception ex)
        {
            LogDidReceiveInvitationError(peerID.DisplayName, ex);
        }
    }

    #endregion Advertising

    #region Discovery

    Task PlatformStartDiscoveringAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var myPeerId = LocalPeerIdentityStore.GetLocalPeerId(Options.DisplayName);

        _mcBrowser = new MCNearbyServiceBrowser(
            myPeerID: myPeerId,
            serviceType: Options.ServiceId)
        {
            Delegate = new BrowserDelegate(this)
        };

        _mcBrowser.StartBrowsingForPeers();

        return Task.CompletedTask;
    }

    void PlatformStopDiscovering()
    {
        _mcBrowser?.StopBrowsingForPeers();
        _mcBrowser?.Dispose();
        _mcBrowser = null;
    }

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        try
        {
            var device = RemotePeers.TrackRemotePeer(PeerKeyProvider, peerID, _logger);

            LogDeviceFound(device.Id, device.DisplayName);

            WriteDeviceFound(device);
        }
        catch (Exception ex)
        {
            LogFoundPeerError(peerID.DisplayName, ex);
        }
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        try
        {
            var id = PeerKeyProvider.PeerKey(peerID);

            if (_activeConnections.ContainsKey(id))
            {
                if (RemotePeers.TryGetDevice(id, out var existingDevice))
                {
                    LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
                }
                return;
            }

            var device = RemotePeers.RemoveRemotePeer(id, _logger);

            LogDeviceLost(id, device?.DisplayName);

            if (device is not null)
            {
                WriteDeviceLost(device);
            }
        }
        catch (Exception ex)
        {
            LogLostPeerError(peerID.DisplayName, ex);
        }
    }

    #endregion Discovery

    Task PlatformInitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!RemotePeers.TryGetHandle(device.Id, out var peerID))
        {
            LogNoPeerFoundForDevice(device.Id, device.DisplayName);
            FaultConnectionTcs(device.Id, new InvalidOperationException(
                $"Cannot connect: device '{device.DisplayName}' (Id={device.Id}) is not currently visible. Ensure it is actively advertising and within range."));
            return Task.CompletedTask;
        }

        MCSession session;
        lock (_sessionLock)
        {
            _session ??= new MCSession(
                LocalPeerIdentityStore.GetLocalPeerId(Options.DisplayName),
                identity: null!,
                Options.ToPlatformEncryptionPreference())
            {
                Delegate = new SessionDelegate(this)
            };
            session = _session;
        }

        _mcBrowser?.InvitePeer(peerID, session, context: null, Options.InvitationTimeout.TotalSeconds);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Nothing to clean up on iOS: <c>InvitePeer</c> is given the same
    /// <see cref="NearbyConnectionsOptions.InvitationTimeout"/>, so MultipeerConnectivity expires
    /// the invitation itself and reports the peer as <c>NotConnected</c>.
    /// </summary>
    /// <remarks>
    /// The shared timeout still applies here rather than being Android-only, because MPC's
    /// <c>Connecting</c> state can hang indefinitely with neither terminal callback arriving
    /// (documented on Wi-Fi-enabled-but-unassociated devices). The plugin-owned deadline is the
    /// only thing that rescues that case.
    /// </remarks>
#pragma warning disable CA1822, S2325
    Task PlatformAbandonConnectAsync(NearbyDevice device) => Task.CompletedTask;
#pragma warning restore CA1822, S2325

    Task SendBytesAsync(
        string peerId,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MCSession? session;
        lock (_sessionLock)
        {
            session = _session;
        }

        if (session is null)
        {
            throw new NearbyConnectionsException("No active session. Ensure a connection has been established before sending data.");
        }

        if (!RemotePeers.TryGetHandle(peerId, out var peerID))
        {
            throw new NearbyConnectionsException($"No peer found for device: Id={peerId}");
        }

        using var nsData = NSData.FromArray(bytes);
        session.SendData(nsData, [peerID], MCSessionSendDataMode.Reliable, out var error);

        if (error is not null)
        {
            LogSendBytesFailed(peerID.DisplayName, error.LocalizedDescription);
            throw new NearbyConnectionsException($"Failed to send bytes to '{peerID.DisplayName}': {error.LocalizedDescription}");
        }

        return Task.CompletedTask;
    }

    async Task PlatformSendFileAsync(
        string peerId,
        string uri,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MCSession? session;
        lock (_sessionLock)
        {
            session = _session;
        }

        if (session is null)
        {
            throw new NearbyConnectionsException("No active session. Ensure a connection has been established before sending data.");
        }

        if (!RemotePeers.TryGetHandle(peerId, out var peerID))
        {
            throw new NearbyConnectionsException($"No peer found for device: Id={peerId}");
        }

        using var nsUrl = NSUrl.FromFilename(uri);
        using var transfer = new OutgoingTransfer(progress, Options.TransferInactivityTimeout, TimeProvider);
        var resourceName = nsUrl.LastPathComponent ?? Path.GetFileName(uri);
        var sendTask = session.SendResourceAsync(nsUrl, resourceName, peerID, out var nsProgress);
        var payloadId = Interlocked.Increment(ref s_nextPayloadId);

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
                        payloadId: payloadId,
                        bytesTransferred: transferred,
                        totalBytes: nsProgress.TotalUnitCount,
                        NearbyTransferStatus.InProgress));
                });
        }

        // Every terminal path reports the bytes transferred so far against the same total; only the
        // status differs. Success reports the full count, because the transfer completed.
        void Report(NearbyTransferStatus status)
        {
            var total = nsProgress?.TotalUnitCount ?? 0;

            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: payloadId,
                bytesTransferred: status is NearbyTransferStatus.Success
                    ? total
                    : (long)((nsProgress?.FractionCompleted ?? 0) * total),
                totalBytes: total,
                status));
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(() => nsProgress?.Cancel());

            // WaitAsync(linkedCts.Token), not a bare await: MPC's SendResourceAsync completes only
            // when the transfer finishes, so awaiting it directly meant neither the caller's token
            // nor the inactivity token could ever interrupt it. The inactivity catch below was
            // therefore unreachable and a stalled transfer hung forever — contradicting
            // NearbyConnection.SendAsync's documented NearbyTransferTimeoutException. Android
            // already enforces this via transfer.Completion.WaitAsync(linkedCts.Token).
            await sendTask.WaitAsync(linkedCts.Token);

            Report(NearbyTransferStatus.Success);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Report(NearbyTransferStatus.Canceled);
            throw;
        }
        catch (OperationCanceledException) when (transfer.InactivityToken.IsCancellationRequested)
        {
            Report(NearbyTransferStatus.Failure);

            LogSendFileTimeout(peerId, null, Options.TransferInactivityTimeout.TotalSeconds);

            throw new NearbyTransferTimeoutException(
                $"Transfer stalled: no progress received for {Options.TransferInactivityTimeout}.");
        }
        catch (Exception ex)
        {
            Report(NearbyTransferStatus.Failure);

            LogSendFileFailed(peerId, null, ex.Message);
            throw;
        }
        finally
        {
            observer?.Dispose();
        }
    }

    /// <summary>
    /// Reports what would stop advertising or discovery from working right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The condition worth catching on iOS is an invalid <see cref="NearbyConnectionsOptions.ServiceId"/>,
    /// because <c>MCNearbyServiceAdvertiser</c>'s native initializer raises an
    /// <c>NSInvalidArgumentException</c> for one — a fatal native crash that no <c>try</c>/<c>catch</c>
    /// can intercept. Options validation already rejects this at startup; repeating the check here
    /// means a consumer who bypasses the options pipeline still gets a value they can branch on
    /// rather than a crash.
    /// </para>
    /// <para>
    /// Two conditions are deliberately not reported. Multipeer Connectivity needs no Play-services
    /// equivalent, so <see cref="NearbyAvailability.PlayServicesUnavailable"/> never applies. And
    /// Bluetooth power state cannot be read without instantiating a <c>CBCentralManager</c>, which
    /// triggers the system Bluetooth permission prompt — a preflight check that prompts defeats its
    /// own purpose, so <see cref="NearbyAvailability.BluetoothDisabled"/> is never reported here.
    /// </para>
    /// </remarks>
    Task<NearbyAvailability> PlatformCheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var failures = new List<string>();
        ServiceIdRules.Validate(Options.ServiceId, failures);

        if (failures.Count > 0)
        {
            LogAvailabilityInvalidServiceId(Options.ServiceId, string.Join(" ", failures));
            return Task.FromResult(NearbyAvailability.InvalidConfiguration);
        }

        return Task.FromResult(NearbyAvailability.Ready);
    }

    void PlatformDispose()
    {
        PlatformStopAdvertising();
        PlatformStopDiscovering();

        foreach (var (_, observer) in _progressObservers)
        {
            observer.Dispose();
        }
        _progressObservers.Clear();
        RemotePeers.Clear();

        MCSession? sessionToDispose;
        lock (_sessionLock)
        {
            sessionToDispose = _session;
            _session = null;
        }

        if (sessionToDispose is not null)
        {
            sessionToDispose.Disconnect();
            sessionToDispose.Dispose();
        }
    }

    #region Session Callbacks

    public void OnPeerStateChanged(MCPeerID peerID, MCSessionState state)
    {
        try
        {
            var id = PeerKeyProvider.PeerKey(peerID);
            var st = Enum.GetName(state);

            LogPeerStateChanged(id, peerID.DisplayName, st);

            switch (state)
            {
                case MCSessionState.Connected:
                    var connectedDevice = RemotePeers.TrackRemotePeer(PeerKeyProvider, peerID, _logger);

                    var receiveChannel = NewChannel<NearbyPayload>(singleReader: true);

                    var connection = new NearbyConnection(
                        connectedDevice,
                        receiveChannel,
                        sendBytesFactory: (data, ct) => new ValueTask(SendBytesAsync(id, data, ct)),
                        sendFileFactory: (fileUri, progress, ct) => PlatformSendFileAsync(id, fileUri, progress, ct),
                        disposeFactory: async () =>
                        {
                            MCSession? disposeSession;
                            lock (_sessionLock)
                            {
                                disposeSession = _session;
                            }

                            if (disposeSession is not null && RemotePeers.TryGetHandle(id, out var peer))
                            {
                                using var controlData = NSData.FromArray(ControlMessage.Encode(ControlMessageType.Disconnect));
                                disposeSession.SendData(controlData, [peer], MCSessionSendDataMode.Reliable, out _);
                            }

                            RemotePeers.RemoveRemotePeer(id, _logger);
                            if (_activeConnections.TryRemove(id, out var removed))
                            {
                                removed.CompleteReceive();
                            }

                            _unobservedWarned.TryRemove(id, out _);
                        });

                    ResolveConnectionTcs(id, connection);
                    break;

                case MCSessionState.NotConnected:
                    // CompleteReceive() is also called by the disposeFactory if the consumer calls DisposeAsync() first.
                    // TryRemove returns false in that case, so CompleteReceive() is never called twice.
                    if (_activeConnections.TryRemove(id, out var disconnectedConnection))
                    {
                        disconnectedConnection.CompleteReceive();
                    }

                    _unobservedWarned.TryRemove(id, out _);

                    // A pending _connectionTcs entry means this peer never reached Connected -
                    // the handshake itself failed or was rejected by the native layer. Without
                    // this, both AcceptAsync (advertiser) and ConnectAsync (discoverer) hang
                    // forever awaiting a TCS that nothing will ever resolve or fault, since only
                    // the Connected case above calls ResolveConnectionTcs.
                    FaultConnectionTcs(id, new NearbyConnectionsException(
                        $"Connection to peer '{peerID.DisplayName}' failed: session state changed to NotConnected before the connection was established."));

                    // MPC fires NotConnected for the departing peer before removing it from
                    // ConnectedPeers, so check whether this peer was the only remaining one
                    // while it is still present in the session's list.
                    MCSession? sessionToDisposePeer;
                    lock (_sessionLock)
                    {
                        // Length > 0 guard: Enumerable.All returns true for an empty sequence, so
                        // without it a NotConnected for a peer that never reached Connected (a
                        // failed or rejected handshake — the very path that reaches this code via
                        // the FaultConnectionTcs call above) disposed the session out from under
                        // other still-connected peers. The comment below holds only for peers that
                        // were actually in ConnectedPeers to begin with.
                        var connectedPeers = _session?.ConnectedPeers ?? [];
                        var isLastPeer = _session is not null
                            && connectedPeers.Length > 0
                            && connectedPeers.All(p => PeerKeyProvider.PeerKey(p) == id);
                        sessionToDisposePeer = isLastPeer ? _session : null;
                        if (isLastPeer)
                        {
                            _session = null;
                        }
                    }

                    RemotePeers.RemoveRemotePeer(id, _logger);

                    if (sessionToDisposePeer is not null)
                    {
                        LogSessionDisposed();
                        sessionToDisposePeer.Dispose();
                    }
                    break;

                case MCSessionState.Connecting:
                    // Connection in progress - no action needed
                    break;
            }
        }
        catch (Exception ex)
        {
            LogOnPeerStateChangedError(peerID.DisplayName, ex);
        }
    }

    void OnDataReceived(NSData data, MCPeerID peerID)
    {
        try
        {
            var id = PeerKeyProvider.PeerKey(peerID);

            LogDataReceived(id, peerID.DisplayName, (long)data.Length);

            var bytes = data.ToArray();

            if (ControlMessage.TryDecode(bytes, out var controlType))
            {
                var c = Enum.GetName(controlType);
                LogControlMessageReceived(id, peerID.DisplayName, c);
                HandleControlMessage(controlType);
                return;
            }

            var payload = new BytesPayload(bytes);
            WritePayload(id, payload);
        }
        catch (Exception ex)
        {
            LogOnDataReceivedError(peerID.DisplayName, ex);
        }
    }

    void HandleControlMessage(ControlMessageType type)
    {
        switch (type)
        {
            case ControlMessageType.Disconnect:
                LogDisconnectingFromSession();
                MCSession? sessionToDisconnect;
                lock (_sessionLock)
                {
                    sessionToDisconnect = _session;
                }
                sessionToDisconnect?.Disconnect();
                break;
            default:
                LogUnknownControlMessageType(type);
                break;
        }
    }

    void OnResourceStarted(string resourceName, MCPeerID fromPeer, NSProgress progress)
    {
        var id = PeerKeyProvider.PeerKey(fromPeer);

        LogResourceReceiveStarted(id, fromPeer.DisplayName, resourceName);

        var observer = progress.AddObserver(
            "fractionCompleted",
            NSKeyValueObservingOptions.New,
            _ =>
            {
                if (_activeConnections.TryGetValue(id, out var conn) && conn.InboundProgress is { } inboundProgress)
                {
                    var transferred = (long)(progress.FractionCompleted * progress.TotalUnitCount);
                    inboundProgress.Report(new NearbyTransferProgress(
                        payloadId: 0,
                        bytesTransferred: transferred,
                        totalBytes: progress.TotalUnitCount,
                        NearbyTransferStatus.InProgress));
                }
            });

        // Key by peer + resource name. Keying by resourceName alone meant two peers sending the
        // same filename concurrently overwrote each other's entry: the first observer was orphaned
        // (a leaked KVO registration on a native NSProgress, never disposed) and the first
        // OnResourceFinished disposed the second transfer's observer, silently ending its progress.
        _progressObservers[ObserverKey(id, resourceName)] = observer;
    }

    static string ObserverKey(string peerId, string resourceName) => $"{peerId}{resourceName}";

    void OnResourceFinished(
        string resourceName,
        MCPeerID fromPeer,
        NSUrl? localUrl,
        NSError? error)
    {
        try
        {
            var id = PeerKeyProvider.PeerKey(fromPeer);
            var loc = localUrl?.ToString() ?? "null";

            LogResourceReceiveFinished(id, fromPeer.DisplayName, resourceName, loc, error?.LocalizedDescription);

            if (_progressObservers.TryRemove(ObserverKey(id, resourceName), out var observer))
            {
                observer.Dispose();
            }

            if (error is not null)
            {
                LogFileCopyFailed(resourceName, "n/a", error.LocalizedDescription);
                return;
            }

            if (localUrl?.Path is not string sourcePath)
            {
                LogFileCopyFailed(resourceName, "n/a", "Resource URL has no file path.");
                return;
            }

            var destinationPath = ResolveUniqueDestinationPath(Options.ReceivedFilesDirectory, resourceName);

            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: false);
            }
            catch (Exception ex)
            {
                LogFileCopyFailed(sourcePath, destinationPath, ex.Message);
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
                    LogFileDeleteFailed(sourcePath, ex.Message);
                }
            }

            var payload = new FilePayload(new FileResult(destinationPath));
            WritePayload(id, payload);
        }
        catch (Exception ex)
        {
            LogOnResourceFinishedError(fromPeer.DisplayName, ex);
        }
    }

    #endregion Session Callbacks

    sealed class AdvertiserDelegate(NearbyConnectionsImplementation nearbyConnections) : NSObject, IMCNearbyServiceAdvertiserDelegate
    {
#pragma warning disable S1144, S1172
        public void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
            => nearbyConnections.DidNotStartAdvertisingPeer(advertiser, error);

        public void DidReceiveInvitationFromPeer(
            MCNearbyServiceAdvertiser advertiser,
            MCPeerID peerID,
            NSData? context,
            MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
            => nearbyConnections.DidReceiveInvitationFromPeer(advertiser, peerID, context, invitationHandler);
#pragma warning restore S1144, S1172
    }

    sealed class BrowserDelegate(NearbyConnectionsImplementation nearbyConnections) : NSObject, IMCNearbyServiceBrowserDelegate
    {
#pragma warning disable S1144, S1172
        public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
            => nearbyConnections.FoundPeer(browser, peerID, info);

        public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
            => nearbyConnections.LostPeer(browser, peerID);

        public void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
            => nearbyConnections.DidNotStartBrowsingForPeers(browser, error);
#pragma warning restore S1144, S1172
    }

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
