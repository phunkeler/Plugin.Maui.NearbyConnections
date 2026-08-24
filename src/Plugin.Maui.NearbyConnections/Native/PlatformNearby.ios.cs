namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    static long s_nextPayloadId;

    readonly ConcurrentDictionary<string, IDisposable> _progressObservers = [];
    readonly Lock _sessionLock = new();

    MCNearbyServiceAdvertiser? _mcAdvertiser;
    MCNearbyServiceBrowser? _mcBrowser;
    MCSession? _session;
    MCPeerID? _localPeerId;

    /// <summary>
    /// This device's own <see cref="MCPeerID"/>, created once and reused for the lifetime of the
    /// platform layer. Every <see cref="MCSession"/>, advertiser, and browser must be built with
    /// the same instance: MultipeerConnectivity treats two <see cref="MCPeerID"/> values as
    /// different peers even when their display names match.
    /// </summary>
    internal MCPeerID GetLocalPeerId()
    {
        lock (_sessionLock)
        {
            if (_localPeerId is null)
            {
                LogCreatedLocalPeer(_options.DisplayName);
            }

            return _localPeerId ??= new MCPeerID(_options.DisplayName);
        }
    }

    #region Advertising

    async Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var myPeerId = GetLocalPeerId();

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
            var device = PeerLookup.Track(peerID);
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
                            GetLocalPeerId(),
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
                            ct).ConfigureAwait(false);
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
                    DisposeSessionIfIdle();
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

        var myPeerId = GetLocalPeerId();

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
            var device = PeerLookup.Track(peerID);

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
            var id = PeerLookup.PeerKey(peerID);

            if (_activeConnections.ContainsKey(id))
            {
                if (PeerLookup.TryGetDevice(id, out var existingDevice))
                {
                    LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
                }
                return;
            }

            var device = PeerLookup.Remove(id);

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

        if (!PeerLookup.TryGetHandle(device.Id, out var peerID))
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
                GetLocalPeerId(),
                identity: null!,
                _options.ToPlatformEncryptionPreference())
            {
                Delegate = new SessionDelegate(this)
            };
            session = _session;
        }

        _mcBrowser?.InvitePeer(peerID, session, context: null, ToInvitationTimeout(_options.ConnectTimeout));

        return Task.CompletedTask;
    }

    static double ToInvitationTimeout(TimeSpan connectTimeout)
        => connectTimeout == Timeout.InfiniteTimeSpan
            ? TimeSpan.FromDays(1).TotalSeconds
            : connectTimeout.TotalSeconds;

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

        if (!PeerLookup.TryGetHandle(peerId, out var peerID))
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

        if (!PeerLookup.TryGetHandle(peerId, out var peerID))
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
                    try
                    {
                        var total = nsProgress.TotalUnitCount;
                        var transferred = (long)(nsProgress.FractionCompleted * total);

                        LogResourceTransferProgress(peerId, "Outbound", payloadId, total, transferred);

                        transfer.OnUpdate(new NearbyTransferProgress(
                            payloadId: payloadId,
                            bytesTransferred: transferred,
                            totalBytes: total,
                            NearbyTransferStatus.InProgress));
                    }
                    catch (Exception ex)
                    {
                        LogCallbackError(nameof(PlatformSendFileAsync), peerId, ex);
                    }
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

            await sendTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);

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
        catch (Exception ex) when (ex is not NearbyException)
        {
            Report(NearbyTransferStatus.Failure);
            LogSendFileFailed(peerId, null, ex);

            throw new NearbyTransferException(
                $"Failed to send file to '{peerId}'.", ex);
        }
        finally
        {
            observer?.Dispose();
            _ = transfer.Completion.Exception;
        }
    }

    Task<NearbyAvailability> PlatformCheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var failures = new List<string>();
        ServiceIdRules.Validate(_options.ServiceId, suggestion: null, failures);

        if (failures.Count > 0)
        {
            LogAvailabilityInvalidServiceId(_options.ServiceId, string.Join(" ", failures));
            return Task.FromResult(NearbyAvailability.InvalidConfiguration);
        }

        return Task.FromResult(NearbyAvailability.Ready);
    }

    internal static partial string StagingDirectory => Path.Combine(FileSystem.CacheDirectory, StagingDirectoryName);

    void PlatformSweepStaging() => SweepStagingDirectory(StagingDirectory);

    // Nothing to drain: the inbound file path here is a synchronous File.Copy on the delegate
    // queue, so by the time disposal runs no copy is in flight. The Android half has a real
    // implementation because its copy is asynchronous.
    static Task PlatformDrainPayloadCompletionAsync() => Task.CompletedTask;

    // Nothing to drain per connection either, for the same reason. Cannot be static: it implements
    // a partial declared on the instance, which Android's half needs.
#pragma warning disable CA1822
    private partial ValueTask PlatformDrainConnectionAsync(string peerId) => ValueTask.CompletedTask;
#pragma warning restore CA1822

    void PlatformDispose()
    {
        PlatformStopAdvertising();
        PlatformStopDiscovering();

        foreach (var (_, observer) in _progressObservers)
        {
            observer.Dispose();
        }
        _progressObservers.Clear();
        PeerLookup.Clear();

        MCSession? sessionToDispose;
        MCPeerID? localPeerToDispose;
        lock (_sessionLock)
        {
            sessionToDispose = _session;
            _session = null;
            localPeerToDispose = _localPeerId;
            _localPeerId = null;
        }

        if (sessionToDispose is not null)
        {
            sessionToDispose.Disconnect();
            sessionToDispose.Dispose();
        }

        localPeerToDispose?.Dispose();
    }

    #region Session Callbacks

    internal void OnPeerStateChanged(MCPeerID peerID, MCSessionState state)
    {
        try
        {
            var id = PeerLookup.PeerKey(peerID);

            LogPeerStateChanged(id, peerID.DisplayName, state);

            switch (state)
            {
                case MCSessionState.Connected:
                    var connectedDevice = PeerLookup.Track(peerID);
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

                            if (disposeSession is not null && PeerLookup.TryGetHandle(id, out var peer))
                            {
                                using var controlData = NSData.FromArray(ControlMessage.Encode(ControlMessageType.Disconnect));
                                disposeSession.SendData(controlData, [peer], MCSessionSendDataMode.Reliable, out _);
                            }

                            await ReleaseConnectionAsync(id).ConfigureAwait(false);
                            DisposeSessionIfIdle();
                        });

                    ResolveConnectionTcs(id, connection);
                    break;

                case MCSessionState.NotConnected:
                    // A delegate callback: the signature is fixed, so the release is tracked
                    // rather than awaited.
                    ReleaseConnectionFromCallback(id);
                    FaultConnectionTcs(id, new NearbyException(
                        $"Connection to peer '{peerID.DisplayName}' failed: session state changed to NotConnected before the connection was established."));

                    if (PeerLookup.Remove(id) is { } lostDevice)
                    {
                        WriteDeviceLost(lostDevice);
                    }

                    DisposeSessionIfIdle();
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
            var id = PeerLookup.PeerKey(peerID);

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
                ReleaseConnectionFromCallback(peerId);
                DisposeSessionIfIdle();
                break;
            default:
                LogUnknownControlMessageType(type);
                break;
        }
    }

    void DisposeSessionIfIdle()
    {
        MCSession? sessionToDispose;

        lock (_sessionLock)
        {
            sessionToDispose = _session is not null && _activeConnections.IsEmpty && _connectionTcs.IsEmpty
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
        try
        {
            var id = PeerLookup.PeerKey(fromPeer);

            LogResourceReceiveStarted(id, fromPeer.DisplayName, resourceName);

            var observer = progress.AddObserver(
                "fractionCompleted",
                NSKeyValueObservingOptions.New,
                _ =>
                {
                    try
                    {
                        // TotalUnitCount is an Objective-C property read. Hoist it: this callback
                        // fires on every change to fractionCompleted, and the value is fixed for
                        // the transfer.
                        var total = progress.TotalUnitCount;
                        var transferred = (long)(progress.FractionCompleted * total);

                        LogResourceTransferProgress(id, "Inbound", 0, total, transferred);

                        if (_activeConnections.TryGetValue(id, out var conn) && conn.InboundProgress is { } inboundProgress)
                        {
                            inboundProgress.Report(new NearbyTransferProgress(
                                payloadId: 0,
                                bytesTransferred: transferred,
                                totalBytes: total,
                                NearbyTransferStatus.InProgress));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogCallbackError(nameof(OnResourceStarted), fromPeer.DisplayName, ex);
                    }
                });

            _progressObservers[ObserverKey(id, resourceName)] = observer;
        }
        catch (Exception ex)
        {
            LogCallbackError(nameof(OnResourceStarted), fromPeer.DisplayName, ex);
        }
    }

    static string ObserverKey(string peerId, string resourceName) => $"{peerId}{resourceName}";

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
            var id = PeerLookup.PeerKey(fromPeer);
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

            string destinationPath;

            // Deliberately synchronous. MultipeerConnectivity deletes its temp file once this
            // delegate returns, so the file must be consumed before then. A same-volume move is an
            // O(1) rename, which is why this costs nothing on the delegate's serial queue — and
            // that queue is also what keeps per-peer payload order. Making this async would break
            // both guarantees at once: re-introduce staging first.
            try
            {
                // The claim is held as a zero-byte file across the move: releasing it before the
                // move would reopen the race it exists to close, so overwrite it in place.
                using (var claim = ClaimUniqueDestinationPath(StagingDirectory, resourceName))
                {
                    destinationPath = claim.Name;
                }

                File.Move(sourcePath, destinationPath, overwrite: true);
            }
            catch (Exception ex)
            {
                LogFileCopyFailed(sourcePath, StagingDirectory, ex);
                return;
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