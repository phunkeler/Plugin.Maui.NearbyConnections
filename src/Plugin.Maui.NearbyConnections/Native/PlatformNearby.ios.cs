using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    static long s_nextPayloadId;

    readonly ConcurrentDictionary<string, IDisposable> _progressObservers = [];
    readonly Lock _sessionLock = new();

    MCNearbyServiceAdvertiser? _mcAdvertiser;
    MCNearbyServiceBrowser? _mcBrowser;
    MCSession? _session;

    #region Advertising

    async Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var myPeerId = Peers.GetLocalPeerId(_options.DisplayName);

        _mcAdvertiser = new MCNearbyServiceAdvertiser(
            myPeerID: myPeerId,
            info: null,
            serviceType: _options.ServiceId)
        {
            Delegate = new AdvertiserDelegate(this)
        };

        _mcAdvertiser.StartAdvertisingPeer();

        await AwaitStartFailureGraceWindowAsync(_advertiseChannel.Reader.Completion, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Waits up to <see cref="NearbyAppleOptions.StartFailureGraceWindow"/> for a channel's
    /// completion and rethrows a fault that arrives within it. A fault that arrives after the
    /// window — including the <see cref="TimeoutException"/> this method swallows — is left on the
    /// channel for the pump to observe as today's logged, post-start failure.
    /// </summary>
    async Task AwaitStartFailureGraceWindowAsync(Task channelCompletion, CancellationToken cancellationToken)
    {
        try
        {
            await channelCompletion.WaitAsync(_options.Apple.StartFailureGraceWindow, TimeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Started successfully as far as this window can tell; a later fault takes the logged path.
        }
    }

    void PlatformStopAdvertising()
    {
        _mcAdvertiser?.StopAdvertisingPeer();
        _mcAdvertiser?.Dispose();
        _mcAdvertiser = null;
    }

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        var exception = new NearbyAdvertisingException(error.LocalizedDescription, new NSErrorException(error));

        LogDidNotStartAdvertising(exception);

        if (!_advertiseChannel.Writer.TryComplete(exception))
        {
            LogStartAdvertisingFaultDropped();
        }
    }

    internal void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
    {
        var exception = new NearbyDiscoveryException(error.LocalizedDescription, new NSErrorException(error));

        LogDidNotStartBrowsing(exception);

        if (!_discoverChannel.Writer.TryComplete(exception))
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
            var device = Peers.Track(peerID);
            var id = device.Id;

            LogConnectionRequestReceived(device.Id, device.DisplayName);

            var request = new NearbyConnectionRequest(
                device,
                accept: async ct =>
                {
                    MCSession session;
                    lock (_sessionLock)
                    {
                        _session ??= new MCSession(
                            Peers.GetLocalPeerId(_options.DisplayName),
                            identity: null!,
                            _options.ToPlatformEncryptionPreference())
                        {
                            Delegate = new SessionDelegate(this)
                        };
                        session = _session;
                    }

                    var tcs = RegisterConnectionTcs(id, ct);

                    try
                    {
                        return await AwaitHandshakeAsync(
                            device,
                            tcs,
                            ConnectionRole.Acceptor,
                            beforeAwait: _ =>
                            {
                                invitationHandler(true, session);
                                return Task.CompletedTask;
                            },
                            ct);
                    }
                    catch
                    {
                        invitationHandler(false, null);
                        throw;
                    }
                },
                reject: ct =>
                {
                    invitationHandler(false, null);
                    Peers.Remove(id);
                    return Task.CompletedTask;
                });

            WriteConnectionRequest(request);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(DidReceiveInvitationFromPeer), peerID.DisplayName, ex);
        }
    }

    #endregion Advertising

    #region Discovery

    async Task PlatformStartDiscoveryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var myPeerId = Peers.GetLocalPeerId(_options.DisplayName);

        _mcBrowser = new MCNearbyServiceBrowser(
            myPeerID: myPeerId,
            serviceType: _options.ServiceId)
        {
            Delegate = new BrowserDelegate(this)
        };

        _mcBrowser.StartBrowsingForPeers();

        await AwaitStartFailureGraceWindowAsync(_discoverChannel.Reader.Completion, cancellationToken)
            .ConfigureAwait(false);
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
            var device = Peers.Track(peerID);

            LogDeviceFound(device.Id, device.DisplayName);

            WriteDeviceFound(device);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(FoundPeer), peerID.DisplayName, ex);
        }
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        try
        {
            var id = Peers.PeerKey(peerID);

            if (_activeConnections.ContainsKey(id))
            {
                if (Peers.TryGetDevice(id, out var existingDevice))
                {
                    LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
                }
                return;
            }

            var device = Peers.Remove(id);

            LogDeviceLost(id, device?.DisplayName);

            if (device is not null)
            {
                WriteDeviceLost(device);
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(LostPeer), peerID.DisplayName, ex);
        }
    }

    #endregion Discovery

    Task PlatformInitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Peers.TryGetHandle(device.Id, out var peerID))
        {
            LogNoPeerFoundForDevice(device.Id, device.DisplayName);
            FaultConnectionTcs(device.Id, new NearbyException(
                $"Cannot connect: device '{device.DisplayName}' (Id={device.Id}) is not currently visible. Ensure it is actively advertising and within range."));
            return Task.CompletedTask;
        }

        MCSession session;
        lock (_sessionLock)
        {
            _session ??= new MCSession(
                Peers.GetLocalPeerId(_options.DisplayName),
                identity: null!,
                _options.ToPlatformEncryptionPreference())
            {
                Delegate = new SessionDelegate(this)
            };
            session = _session;
        }

        _mcBrowser?.InvitePeer(peerID, session, context: null, _options.ConnectTimeout.TotalSeconds);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Nothing to clean up on iOS, on either handshake path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Outbound: <c>InvitePeer</c> is given the same
    /// <see cref="NearbyOptions.ConnectTimeout"/>, so MultipeerConnectivity expires the
    /// invitation itself and reports the peer as <c>NotConnected</c>.
    /// </para>
    /// <para>
    /// Inbound: the accept path resolves <c>invitationHandler(false, null)</c> in its own catch,
    /// which is the equivalent release, and there is no browser-side invitation to withdraw.
    /// Android's counterpart disconnects the endpoint instead, because GMS refuses a later attempt
    /// to an endpoint left in a half-open state.
    /// </para>
    /// <para>
    /// The shared timeout still applies here rather than being Android-only, because MPC's
    /// <c>Connecting</c> state can hang indefinitely with neither terminal callback arriving
    /// (documented on Wi-Fi-enabled-but-unassociated devices). The plugin-owned deadline is the
    /// only thing that rescues that case.
    /// </para>
    /// </remarks>
#pragma warning disable CA1822, S2325
    Task PlatformAbandonConnectAsync(NearbyDevice device) => Task.CompletedTask;
#pragma warning restore CA1822, S2325

    Task PlatformSendBytesAsync(
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
            throw new NearbyException("No active session. Ensure a connection has been established before sending data.");
        }

        if (!Peers.TryGetHandle(peerId, out var peerID))
        {
            throw new NearbyException($"No peer found for device: Id={peerId}");
        }

        using var nsData = NSData.FromArray(bytes);
        session.SendData(nsData, [peerID], MCSessionSendDataMode.Reliable, out var error);

        if (error is not null)
        {
            var nsErrorException = new NSErrorException(error);
            LogSendBytesFailed(peerID.DisplayName, nsErrorException);
            throw new NearbyTransferException($"Failed to send bytes to '{peerID.DisplayName}': {error.LocalizedDescription}", nsErrorException);
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
            throw new NearbyException("No active session. Ensure a connection has been established before sending data.");
        }

        if (!Peers.TryGetHandle(peerId, out var peerID))
        {
            throw new NearbyException($"No peer found for device: Id={peerId}");
        }

        using var nsUrl = NSUrl.FromFilename(uri);
        using var transfer = new OutgoingTransfer(progress, _options.TransferInactivityTimeout, TimeProvider);
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

            throw TransferInactivityTimeoutException(peerId);
        }
        catch (Exception ex)
        {
            Report(NearbyTransferStatus.Failure);

            LogSendFileFailed(peerId, null, ex);
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
    /// The condition worth catching on iOS is an invalid <see cref="NearbyOptions.ServiceId"/>,
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
        ServiceIdRules.Validate(_options.ServiceId, failures);

        if (failures.Count > 0)
        {
            LogAvailabilityInvalidServiceId(_options.ServiceId, string.Join(" ", failures));
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
        Peers.Clear();

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

    internal void OnPeerStateChanged(MCPeerID peerID, MCSessionState state)
    {
        try
        {
            var id = Peers.PeerKey(peerID);

            LogPeerStateChanged(id, peerID.DisplayName, state);

            switch (state)
            {
                case MCSessionState.Connected:
                    var connectedDevice = Peers.Track(peerID);
                    var receiveChannel = NewChannel<NearbyPayload>(singleReader: true);
                    var connection = new NearbyConnection(
                        connectedDevice,
                        receiveChannel,
                        sendBytes: (data, ct) => PlatformSendBytesAsync(id, data, ct),
                        sendFile: (fileUri, progress, ct) => PlatformSendFileAsync(id, fileUri, progress, ct),
                        dispose: async () =>
                        {
                            MCSession? disposeSession;

                            lock (_sessionLock)
                            {
                                disposeSession = _session;
                            }

                            if (disposeSession is not null && Peers.TryGetHandle(id, out var peer))
                            {
                                using var controlData = NSData.FromArray(ControlMessage.Encode(ControlMessageType.Disconnect));
                                disposeSession.SendData(controlData, [peer], MCSessionSendDataMode.Reliable, out _);
                            }

                            Peers.Remove(id);
                            ReleaseConnection(id);
                        });

                    ResolveConnectionTcs(id, connection);
                    break;

                case MCSessionState.NotConnected:
                    ReleaseConnection(id);
                    FaultConnectionTcs(id, new NearbyException(
                        $"Connection to peer '{peerID.DisplayName}' failed: session state changed to NotConnected before the connection was established."));

                    // Report the loss, not just the local removal: dropping the peer here without
                    // it would leave the session showing a Visible device whose native handle is
                    // already gone, so the row stays on screen and can never be connected to.
                    // The session ignores this for a device it still considers Connected, which is
                    // what keeps a live connection from being evicted by its own state change.
                    if (Peers.Remove(id) is { } lostDevice)
                    {
                        WriteDeviceLost(lostDevice);
                    }

                    DisposeSessionIfLastPeer();
                    break;

                case MCSessionState.Connecting:
                    // Connection in progress - no action needed
                    break;
            }
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnPeerStateChanged), peerID.DisplayName, ex);
        }
    }

    internal void OnDataReceived(NSData data, MCPeerID peerID)
    {
        try
        {
            var id = Peers.PeerKey(peerID);

            LogDataReceived(id, peerID.DisplayName, (long)data.Length);

            var bytes = data.ToArray();

            if (ControlMessage.TryDecode(bytes, out var controlType))
            {
                LogControlMessageReceived(id, peerID.DisplayName, controlType);
                HandleControlMessage(id, controlType);
                return;
            }

            var payload = new NearbyBytesPayload(bytes);
            WritePayload(id, payload);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnDataReceived), peerID.DisplayName, ex);
        }
    }

    void HandleControlMessage(string peerId, ControlMessageType type)
    {
        switch (type)
        {
            case ControlMessageType.Disconnect:
                LogPeerDisconnectRequested(peerId);
                ReleaseConnection(peerId);
                Peers.Remove(peerId);
                DisposeSessionIfLastPeer();
                break;
            default:
                LogUnknownControlMessageType(type);
                break;
        }
    }

    void DisposeSessionIfLastPeer()
    {
        MCSession? sessionToDispose;

        lock (_sessionLock)
        {
            sessionToDispose = _session is not null && Peers.IsEmpty && _connectionTcs.IsEmpty
                ? _session
                : null;

            if (sessionToDispose is not null)
            {
                _session = null;
            }
        }

        if (sessionToDispose is not null)
        {
            LogSessionDisposed();
            sessionToDispose.Disconnect();
            sessionToDispose.Dispose();
        }
    }

    internal void OnResourceStarted(string resourceName, MCPeerID fromPeer, NSProgress progress)
    {
        var id = Peers.PeerKey(fromPeer);

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

    /// <summary>
    /// Disposes every inbound-progress observer still registered for a peer.
    /// </summary>
    /// <remarks>
    /// <c>OnResourceFinished</c> is the normal removal path, but it is not guaranteed to arrive: a
    /// peer that drops mid-transfer goes to <c>NotConnected</c> with no finish callback, which left
    /// a KVO registration live on the native <c>NSProgress</c> until the whole session was disposed.
    /// Matching on the key prefix is safe because <see cref="ObserverKey"/> separates the two halves
    /// with a character that cannot occur in a peer key.
    /// </remarks>
    partial void PlatformReleaseConnection(string peerId) => RemoveProgressObserversFor(peerId);

    void RemoveProgressObserversFor(string peerId)
    {
        var prefix = ObserverKey(peerId, string.Empty);

        foreach (var (key, _) in _progressObservers)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (_progressObservers.TryRemove(key, out var staleObserver))
            {
                staleObserver.Dispose();
            }
        }
    }

    internal void OnResourceFinished(
        string resourceName,
        MCPeerID fromPeer,
        NSUrl? localUrl,
        NSError? error)
    {
        try
        {
            var id = Peers.PeerKey(fromPeer);
            var loc = localUrl?.ToString() ?? "null";

            LogResourceReceiveFinished(id, fromPeer.DisplayName, resourceName, loc, error?.LocalizedDescription);

            if (_progressObservers.TryRemove(ObserverKey(id, resourceName), out var observer))
            {
                observer.Dispose();
            }

            if (error is not null)
            {
                LogFileCopyFailed(resourceName, "n/a", new NSErrorException(error));
                return;
            }

            if (localUrl?.Path is not string sourcePath)
            {
                LogFileCopyFailed(resourceName, "n/a", new InvalidOperationException("Resource URL has no file path."));
                return;
            }

            var destinationPath = ResolveUniqueDestinationPath(_options.ReceivedFilesDirectory, resourceName);

            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: false);
            }
            catch (Exception ex)
            {
                LogFileCopyFailed(sourcePath, destinationPath, ex);
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
                    LogFileDeleteFailed(sourcePath, ex);
                }
            }

            var payload = new NearbyFilePayload(new FileResult(destinationPath));
            WritePayload(id, payload);
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnResourceFinished), fromPeer.DisplayName, ex);
        }
    }

    #endregion Session Callbacks

    sealed class AdvertiserDelegate(PlatformNearby platformNearby) : NSObject, IMCNearbyServiceAdvertiserDelegate
    {
#pragma warning disable S1144, S1172
        public void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
            => platformNearby.DidNotStartAdvertisingPeer(advertiser, error);

        public void DidReceiveInvitationFromPeer(
            MCNearbyServiceAdvertiser advertiser,
            MCPeerID peerID,
            NSData? context,
            MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
            => platformNearby.DidReceiveInvitationFromPeer(advertiser, peerID, context, invitationHandler);
#pragma warning restore S1144, S1172
    }

    sealed class BrowserDelegate(PlatformNearby platformNearby) : NSObject, IMCNearbyServiceBrowserDelegate
    {
#pragma warning disable S1144, S1172
        public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
            => platformNearby.FoundPeer(browser, peerID, info);

        public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
            => platformNearby.LostPeer(browser, peerID);

        public void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
            => platformNearby.DidNotStartBrowsingForPeers(browser, error);
#pragma warning restore S1144, S1172
    }

    sealed class SessionDelegate(PlatformNearby platformNearby) : NSObject, IMCSessionDelegate
    {
#pragma warning disable S1144, S1172
        public void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
            => platformNearby.OnPeerStateChanged(peerID, state);

        public void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            => platformNearby.OnDataReceived(data, peerID);

        public void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
            => platformNearby.OnResourceStarted(resourceName, fromPeer, progress);

        public void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
            => platformNearby.OnResourceFinished(resourceName, fromPeer, localUrl, error);
#pragma warning restore S1144, S1172
    }
}