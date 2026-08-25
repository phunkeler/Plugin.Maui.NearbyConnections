namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The iOS backend: maps <see cref="IPlatformAdapter"/> onto MultipeerConnectivity. Outbound
/// operations go through the interface; inbound delegate callbacks call the bridge's internal
/// methods directly — the surface the device tests drive.
/// </summary>
sealed class IosAdapter : IPlatformAdapter
{
    readonly PlatformNearby _bridge;

    /// <param name="bridge">The shared platform layer this adapter feeds.</param>
    public IosAdapter(PlatformNearby bridge) => _bridge = bridge;

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
                _bridge.LogCreatedLocalPeer(_bridge.Options.DisplayName);
            }

            return _localPeerId ??= new MCPeerID(_bridge.Options.DisplayName);
        }
    }

    #region Advertising

    public async Task StartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var myPeerId = GetLocalPeerId();

        _mcAdvertiser = new MCNearbyServiceAdvertiser(
            myPeerID: myPeerId,
            info: null,
            serviceType: _bridge.Options.ServiceId)
        {
            Delegate = new AdvertiserDelegate(this)
        };

        _mcAdvertiser.StartAdvertisingPeer();

        await AwaitStartFailureGraceWindowAsync(_bridge.AdvertiseChannelCompletion, cancellationToken)
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
            await channelCompletion.WaitAsync(_bridge.Options.Apple.StartFailureGraceWindow, _bridge.TimeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Started successfully as far as this window can tell; a later fault takes the logged path.
        }
    }

    public void StopAdvertising()
    {
        _mcAdvertiser?.StopAdvertisingPeer();
        _mcAdvertiser?.Dispose();
        _mcAdvertiser = null;
    }

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        var exception = new NearbyAdvertisingException(error.LocalizedDescription, new NSErrorException(error));

        _bridge.LogDidNotStartAdvertising(exception);

        if (!_bridge.TryFaultAdvertiseChannel(exception))
        {
            _bridge.LogStartAdvertisingFaultDropped();
        }
    }

    internal void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
    {
        var exception = new NearbyDiscoveryException(error.LocalizedDescription, new NSErrorException(error));

        _bridge.LogDidNotStartBrowsing(exception);

        if (!_bridge.TryFaultDiscoverChannel(exception))
        {
            _bridge.LogStartDiscoveringFaultDropped();
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
            var device = _bridge.PeerLookup.Track(peerID);
            var id = device.Id;

            _bridge.LogConnectionRequestReceived(device.Id, device.DisplayName);

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
                            _bridge.Options.ToPlatformEncryptionPreference())
                        {
                            Delegate = new SessionDelegate(this)
                        };
                        session = _session;
                    }

                    var tcs = _bridge.RegisterConnectionTcs(id, ct);

                    try
                    {
                        return await _bridge.AwaitHandshakeAsync(
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

            _bridge.WriteConnectionRequest(request);
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(DidReceiveInvitationFromPeer), _bridge.PeerLookup.DeviceIdFor(peerID), ex);
        }
    }

    #endregion Advertising

    #region Discovery

    public async Task StartDiscoveryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var myPeerId = GetLocalPeerId();

        _mcBrowser = new MCNearbyServiceBrowser(
            myPeerID: myPeerId,
            serviceType: _bridge.Options.ServiceId)
        {
            Delegate = new BrowserDelegate(this)
        };

        _mcBrowser.StartBrowsingForPeers();

        await AwaitStartFailureGraceWindowAsync(_bridge.DiscoverChannelCompletion, cancellationToken)
            .ConfigureAwait(false);
    }

    public void StopDiscovering()
    {
        _mcBrowser?.StopBrowsingForPeers();
        _mcBrowser?.Dispose();
        _mcBrowser = null;
    }

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        try
        {
            var device = _bridge.PeerLookup.Track(peerID);
            _bridge.OnDeviceFound(device);
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(FoundPeer), _bridge.PeerLookup.DeviceIdFor(peerID), ex);
        }
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        try
        {
            _bridge.OnDeviceLost(_bridge.PeerLookup.DeviceIdFor(peerID));
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(LostPeer), _bridge.PeerLookup.DeviceIdFor(peerID), ex);
        }
    }

    #endregion Discovery

    public Task InitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_bridge.PeerLookup.TryGetHandle(device.Id, out var peerID))
        {
            _bridge.LogNoPeerFoundForDevice(device.Id, device.DisplayName);
            _bridge.FaultConnectionTcs(device.Id, new NearbyException(
                $"Cannot connect: device '{device.DisplayName}' (Id={device.Id}) is not currently visible. Ensure it is actively advertising and within range."));
            return Task.CompletedTask;
        }

        MCSession session;
        lock (_sessionLock)
        {
            _session ??= new MCSession(
                GetLocalPeerId(),
                identity: null!,
                _bridge.Options.ToPlatformEncryptionPreference())
            {
                Delegate = new SessionDelegate(this)
            };
            session = _session;
        }

        _mcBrowser?.InvitePeer(peerID, session, context: null, ToInvitationTimeout(_bridge.Options.ConnectTimeout));

        return Task.CompletedTask;
    }

    static double ToInvitationTimeout(TimeSpan connectTimeout)
        => connectTimeout == Timeout.InfiniteTimeSpan
            ? TimeSpan.FromDays(1).TotalSeconds
            : connectTimeout.TotalSeconds;

#pragma warning disable CA1822, S2325
    public Task AbandonConnectAsync(NearbyDevice device) => Task.CompletedTask;
#pragma warning restore CA1822, S2325

    internal Task SendBytesAsync(
        string deviceId,
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

        if (!_bridge.PeerLookup.TryGetHandle(deviceId, out var peerID))
        {
            throw new NearbyException($"No peer found for device: Id={deviceId}");
        }

        using var nsData = NSData.FromArray(bytes);
        session.SendData(nsData, [peerID], MCSessionSendDataMode.Reliable, out var error);

        if (error is not null)
        {
            var nsErrorException = new NSErrorException(error);
            var name = _bridge.PeerLookup.SafeDisplayName(deviceId, peerID);

            _bridge.LogSendBytesFailed(name, nsErrorException);
            throw new NearbyTransferException($"Failed to send bytes to '{name}': {error.LocalizedDescription}", nsErrorException);
        }

        return Task.CompletedTask;
    }

    internal async Task SendFileAsync(
        string deviceId,
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

        if (!_bridge.PeerLookup.TryGetHandle(deviceId, out var peerID))
        {
            throw new NearbyException($"No peer found for device: Id={deviceId}");
        }

        using var nsUrl = NSUrl.FromFilename(uri);
        using var transfer = new OutgoingTransfer(progress, _bridge.Options.TransferInactivityTimeout, _bridge.TimeProvider);
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

                        _bridge.LogResourceTransferProgress(deviceId, "Outbound", payloadId, total, transferred);

                        transfer.OnUpdate(new NearbyTransferProgress(
                            payloadId: payloadId,
                            bytesTransferred: transferred,
                            totalBytes: total,
                            NearbyTransferStatus.InProgress));
                    }
                    catch (Exception ex)
                    {
                        _bridge.LogCallbackError(nameof(SendFileAsync), deviceId, ex);
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
            await _bridge.AwaitFileTransferAsync(
                deviceId,
                transfer,
                sendTask,
                Report,
                cancelPlatformTransfer: () => nsProgress?.Cancel(),
                cancellationToken).ConfigureAwait(false);

            Report(NearbyTransferStatus.Success);
        }
        finally
        {
            observer?.Dispose();
        }
    }

    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var failures = new List<string>();
        ServiceIdRules.Validate(_bridge.Options.ServiceId, suggestion: null, failures);

        if (failures.Count > 0)
        {
            _bridge.LogAvailabilityInvalidServiceId(_bridge.Options.ServiceId, string.Join(" ", failures));
            return Task.FromResult(NearbyAvailability.InvalidConfiguration);
        }

        return Task.FromResult(NearbyAvailability.Ready);
    }

    /// <inheritdoc/>
    public string StagingDirectory => PlatformNearby.StagingDirectory;

    public void SweepStaging() => _bridge.SweepStagingDirectory(PlatformNearby.StagingDirectory);

    public void Dispose()
    {
        StopAdvertising();
        StopDiscovering();

        foreach (var (_, observer) in _progressObservers)
        {
            observer.Dispose();
        }
        _progressObservers.Clear();
        _bridge.PeerLookup.Clear();

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
            var id = _bridge.PeerLookup.DeviceIdFor(peerID);
            var name = _bridge.PeerLookup.SafeDisplayName(id, peerID);

            _bridge.LogPeerStateChanged(id, name, state);

            switch (state)
            {
                case MCSessionState.Connected:
                    var connectedDevice = _bridge.PeerLookup.Track(peerID);
                    _bridge.CompleteHandshake(
                        connectedDevice,
                        sendBytes: (data, ct) => SendBytesAsync(id, data, ct),
                        sendFile: (fileUri, progress, ct) => SendFileAsync(id, fileUri, progress, ct),
                        dispose: async () =>
                        {
                            MCSession? disposeSession;

                            lock (_sessionLock)
                            {
                                disposeSession = _session;
                            }

                            if (disposeSession is not null && _bridge.PeerLookup.TryGetHandle(id, out var peer))
                            {
                                using var controlData = NSData.FromArray(ControlMessage.Encode(ControlMessageType.Disconnect));
                                disposeSession.SendData(controlData, [peer], MCSessionSendDataMode.Reliable, out _);
                            }

                            await _bridge.ReleaseConnectionAsync(id).ConfigureAwait(false);
                            DisposeSessionIfIdle();
                        });
                    break;

                case MCSessionState.NotConnected:
                    // A delegate callback: the signature is fixed, so the release is tracked
                    // rather than awaited.
                    _bridge.ReleaseConnectionFromCallback(id);
                    _bridge.FaultConnectionTcs(id, new NearbyException(
                        $"Connection to peer '{_bridge.PeerLookup.SafeDisplayName(peerID)}' failed: session state changed to NotConnected before the connection was established."));

                    if (_bridge.PeerLookup.Remove(id) is { } lostDevice)
                    {
                        _bridge.WriteDeviceLost(lostDevice);
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
            _bridge.LogCallbackError(nameof(OnPeerStateChanged), _bridge.PeerLookup.DeviceIdFor(peerID), ex);
        }
    }

    internal void OnDataReceived(NSData data, MCPeerID peerID)
    {
        try
        {
            var id = _bridge.PeerLookup.DeviceIdFor(peerID);
            var name = _bridge.PeerLookup.SafeDisplayName(id, peerID);

            _bridge.LogDataReceived(id, name, (long)data.Length);

            var bytes = data.ToArray();

            if (ControlMessage.TryDecode(bytes, out var controlType))
            {
                _bridge.LogControlMessageReceived(id, name, controlType);
                HandleControlMessage(id, controlType);
                return;
            }

            var payload = new NearbyBytesPayload(bytes);
            _bridge.WritePayload(id, payload);
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnDataReceived), _bridge.PeerLookup.DeviceIdFor(peerID), ex);
        }
    }

    void HandleControlMessage(string deviceId, ControlMessageType type)
    {
        switch (type)
        {
            case ControlMessageType.Disconnect:
                _bridge.LogPeerDisconnectRequested(deviceId);
                _bridge.ReleaseConnectionFromCallback(deviceId);
                DisposeSessionIfIdle();
                break;
            default:
                _bridge.LogUnknownControlMessageType(type);
                break;
        }
    }

    void DisposeSessionIfIdle()
    {
        MCSession? sessionToDispose;

        lock (_sessionLock)
        {
            sessionToDispose = _session is not null && _bridge._activeConnections.IsEmpty && _bridge._connectionTcs.IsEmpty
                ? _session
                : null;

            if (sessionToDispose is not null)
            {
                _session = null;
            }
        }

        if (sessionToDispose is not null)
        {
            _bridge.LogSessionDisposed();
            sessionToDispose.Disconnect();
            sessionToDispose.Dispose();
        }
    }

    internal void OnResourceStarted(string resourceName, MCPeerID fromPeer, NSProgress progress)
    {
        try
        {
            var id = _bridge.PeerLookup.DeviceIdFor(fromPeer);
            var name = _bridge.PeerLookup.SafeDisplayName(id, fromPeer);

            _bridge.LogResourceReceiveStarted(id, name, resourceName);

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

                        _bridge.LogResourceTransferProgress(id, "Inbound", 0, total, transferred);

                        if (_bridge._activeConnections.TryGetValue(id, out var conn) && conn.InboundProgress is { } inboundProgress)
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
                        _bridge.LogCallbackError(nameof(OnResourceStarted), _bridge.PeerLookup.DeviceIdFor(fromPeer), ex);
                    }
                });

            _progressObservers[ObserverKey(id, resourceName)] = observer;
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnResourceStarted), _bridge.PeerLookup.DeviceIdFor(fromPeer), ex);
        }
    }

    static string ObserverKey(string deviceId, string resourceName) => $"{deviceId}{resourceName}";

    public void ReleaseConnection(string deviceId) => RemoveProgressObserversFor(deviceId);

    void RemoveProgressObserversFor(string deviceId)
    {
        var prefix = ObserverKey(deviceId, string.Empty);

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
            var id = _bridge.PeerLookup.DeviceIdFor(fromPeer);
            var loc = localUrl?.ToString() ?? "null";
            var name = _bridge.PeerLookup.SafeDisplayName(id, fromPeer);

            _bridge.LogResourceReceiveFinished(id, name, resourceName, loc, error?.LocalizedDescription);

            if (_progressObservers.TryRemove(ObserverKey(id, resourceName), out var observer))
            {
                observer.Dispose();
            }

            if (error is not null)
            {
                _bridge.LogFileCopyFailed(resourceName, "n/a", new NSErrorException(error));
                return;
            }

            if (localUrl?.Path is not string sourcePath)
            {
                _bridge.LogFileCopyFailed(resourceName, "n/a", new InvalidOperationException("Resource URL has no file path."));
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
                using (var claim = PlatformNearby.ClaimUniqueDestinationPath(PlatformNearby.StagingDirectory, resourceName))
                {
                    destinationPath = claim.Name;
                }

                File.Move(sourcePath, destinationPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _bridge.LogFileCopyFailed(sourcePath, PlatformNearby.StagingDirectory, ex);
                return;
            }

            var payload = new NearbyFilePayload(new FileResult(destinationPath));
            _bridge.WritePayload(id, payload);
        }
        catch (Exception ex)
        {
            _bridge.LogCallbackError(nameof(OnResourceFinished), _bridge.PeerLookup.DeviceIdFor(fromPeer), ex);
        }
    }

    #endregion Session Callbacks

    sealed class AdvertiserDelegate(IosAdapter adapter) : NSObject, IMCNearbyServiceAdvertiserDelegate
    {
#pragma warning disable S1144, S1172
        public void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
            => adapter.DidNotStartAdvertisingPeer(advertiser, error);

        public void DidReceiveInvitationFromPeer(
            MCNearbyServiceAdvertiser advertiser,
            MCPeerID peerID,
            NSData? context,
            MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
            => adapter.DidReceiveInvitationFromPeer(advertiser, peerID, context, invitationHandler);
#pragma warning restore S1144, S1172
    }

    sealed class BrowserDelegate(IosAdapter adapter) : NSObject, IMCNearbyServiceBrowserDelegate
    {
#pragma warning disable S1144, S1172
        public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
            => adapter.FoundPeer(browser, peerID, info);

        public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
            => adapter.LostPeer(browser, peerID);

        public void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
            => adapter.DidNotStartBrowsingForPeers(browser, error);
#pragma warning restore S1144, S1172
    }

    sealed class SessionDelegate(IosAdapter adapter) : NSObject, IMCSessionDelegate
    {
#pragma warning disable S1144, S1172
        public void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
            => adapter.OnPeerStateChanged(peerID, state);

        public void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            => adapter.OnDataReceived(data, peerID);

        public void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
            => adapter.OnResourceStarted(resourceName, fromPeer, progress);

        public void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
            => adapter.OnResourceFinished(resourceName, fromPeer, localUrl, error);
#pragma warning restore S1144, S1172
    }
}