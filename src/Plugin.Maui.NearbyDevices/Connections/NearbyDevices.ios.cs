using System.Threading.Channels;

namespace Plugin.Maui.NearbyDevices;

sealed partial class NearbyDevicesImplementation
{
    MCNearbyServiceAdvertiser? _mcAdvertiser;
    MCNearbyServiceBrowser? _mcBrowser;

    readonly ConcurrentDictionary<string, IDisposable> _progressObservers = new();

    static long _nextPayloadId;

    MCSession? _session;
    readonly Lock _sessionLock = new();

    #region Advertising

    Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateBonjourServiceId(Options.ServiceId);

        var myPeerId = PeerIdManager.GetLocalPeerId(Options.DisplayName);

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
        _advertiseChannel.Writer.TryComplete(new NearbyAdvertisingException(error.LocalizedDescription));
    }

    internal void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
    {
        LogDidNotStartBrowsing(error.LocalizedDescription);
        _discoverChannel.Writer.TryComplete(new NearbyDiscoveryException(error.LocalizedDescription));
    }

    internal void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        try
        {
            var id = PeerIdManager.TrackRemotePeer(peerID);

            var device = _deviceManager.RecordDeviceFound(id, peerID.DisplayName);

            LogConnectionRequestReceived(device.Id, device.DisplayName);

            var request = new NearbyConnectionRequest(
                device,
                acceptFactory: async ct =>
                {
                    MCSession session;
                    lock (_sessionLock)
                    {
                        _session ??= new MCSession(
                            PeerIdManager.GetLocalPeerId(Options.DisplayName),
                            identity: null!,
                            Options.EncryptionPreference)
                        {
                            Delegate = new SessionDelegate(this)
                        };
                        session = _session;
                    }

                    invitationHandler(true, session);

                    // Create TCS so OnPeerStateChanged(Connected) can resolve it
                    var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _connectionTcs[id] = (tcs, ct);

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
                    _deviceManager.RemoveDevice(id);
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
        ValidateBonjourServiceId(Options.ServiceId);

        var myPeerId = PeerIdManager.GetLocalPeerId(Options.DisplayName);

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
            var id = PeerIdManager.TrackRemotePeer(peerID);
            var device = _deviceManager.RecordDeviceFound(id, peerID.DisplayName);

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
            var id = PeerIdManager.PeerKey(peerID);

            if (_activeConnections.ContainsKey(id))
            {
                if (_deviceManager.TryGetDevice(id, out var existingDevice))
                {
                    LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
                }
                return;
            }

            PeerIdManager.RemoveRemotePeer(id);
            var device = _deviceManager.RemoveDevice(id);

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

        if (!PeerIdManager.TryGetRemotePeer(device.Id, out var peerID))
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
                PeerIdManager.GetLocalPeerId(Options.DisplayName),
                identity: null!,
                Options.EncryptionPreference)
            {
                Delegate = new SessionDelegate(this)
            };
            session = _session;
        }

        _mcBrowser?.InvitePeer(peerID, session, context: null, Options.InvitationTimeout.TotalSeconds);

        return Task.CompletedTask;
    }

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
            throw new NearbyDevicesException("No active session. Ensure a connection has been established before sending data.");
        }

        if (!PeerIdManager.TryGetRemotePeer(peerId, out var peerID))
        {
            throw new NearbyDevicesException($"No peer found for device: Id={peerId}");
        }

        using var nsData = NSData.FromArray(bytes);
        session.SendData(nsData, [peerID], MCSessionSendDataMode.Reliable, out var error);

        if (error is not null)
        {
            LogSendBytesFailed(peerID.DisplayName, error.LocalizedDescription);
            throw new NearbyDevicesException($"Failed to send bytes to '{peerID.DisplayName}': {error.LocalizedDescription}");
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
            throw new NearbyDevicesException("No active session. Ensure a connection has been established before sending data.");
        }

        if (!PeerIdManager.TryGetRemotePeer(peerId, out var peerID))
        {
            throw new NearbyDevicesException($"No peer found for device: Id={peerId}");
        }

        using var nsUrl = NSUrl.FromFilename(uri);
        using var transfer = new OutgoingTransfer(progress, Options.TransferInactivityTimeout);
        var resourceName = nsUrl.LastPathComponent ?? Path.GetFileName(uri);
        var sendTask = session.SendResourceAsync(nsUrl, resourceName, peerID, out var nsProgress);
        var payloadId = Interlocked.Increment(ref _nextPayloadId);

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

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(() => nsProgress?.Cancel());
            await sendTask;

            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: payloadId,
                bytesTransferred: nsProgress?.TotalUnitCount ?? 0,
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Success));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: payloadId,
                bytesTransferred: (long)((nsProgress?.FractionCompleted ?? 0) * (nsProgress?.TotalUnitCount ?? 0)),
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Canceled));
            throw;
        }
        catch (OperationCanceledException) when (transfer.InactivityToken.IsCancellationRequested)
        {
            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: payloadId,
                bytesTransferred: (long)((nsProgress?.FractionCompleted ?? 0) * (nsProgress?.TotalUnitCount ?? 0)),
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Failure));

            LogSendFileTimeout(peerId, null, Options.TransferInactivityTimeout.TotalSeconds);

            throw new NearbyTransferTimeoutException(
                $"Transfer stalled: no progress received for {Options.TransferInactivityTimeout}.");
        }
        catch (Exception ex)
        {
            transfer.OnUpdate(new NearbyTransferProgress(
                payloadId: payloadId,
                bytesTransferred: (long)((nsProgress?.FractionCompleted ?? 0) * (nsProgress?.TotalUnitCount ?? 0)),
                totalBytes: nsProgress?.TotalUnitCount ?? 0,
                NearbyTransferStatus.Failure));

            LogSendFileFailed(peerId, null, ex.Message);
            throw;
        }
        finally
        {
            observer?.Dispose();
        }
    }

    /// <summary>
    /// Validates that <paramref name="serviceId"/> is a legal Bonjour service type in the form
    /// <c>_&lt;name&gt;._tcp</c> or <c>_&lt;name&gt;._udp</c>, as required by
    /// <see cref="MCNearbyServiceAdvertiser"/> and <see cref="MCNearbyServiceBrowser"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="serviceId"/> is null, empty, or does not match the required format.
    /// </exception>
    static void ValidateBonjourServiceId(string serviceId)
    {
        if (string.IsNullOrEmpty(serviceId)
            || (!serviceId.EndsWith("._tcp", StringComparison.OrdinalIgnoreCase)
                && !serviceId.EndsWith("._udp", StringComparison.OrdinalIgnoreCase))
            || !serviceId.StartsWith('_'))
        {
            throw new ArgumentException(
                $"'{nameof(NearbyDevicesOptions.ServiceId)}' must be a valid Bonjour service type in the form '_<name>._tcp' or '_<name>._udp' (e.g. '_mygame._tcp'). " +
                $"The current value '{serviceId}' is not valid. " +
                $"Set {nameof(NearbyDevicesOptions)}.{nameof(NearbyDevicesOptions.ServiceId)} before calling AdvertiseAsync or DiscoverAsync on iOS.",
                nameof(serviceId));
        }
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
        PeerIdManager.ClearRemotePeers();

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
            var id = PeerIdManager.PeerKey(peerID);
            var st = Enum.GetName(state);

            LogPeerStateChanged(id, peerID.DisplayName, st);

            switch (state)
            {
                case MCSessionState.Connected:
                    var connectedDevice = _deviceManager.RecordDeviceFound(id, peerID.DisplayName);

                    var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(new UnboundedChannelOptions
                    {
                        SingleReader = true,
                        SingleWriter = false,
                    });

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

                            if (disposeSession is not null && PeerIdManager.TryGetRemotePeer(id, out var peer))
                            {
                                using var controlData = NSData.FromArray(ControlMessage.Encode(ControlMessageType.Disconnect));
                                disposeSession.SendData(controlData, [peer], MCSessionSendDataMode.Reliable, out _);
                            }

                            PeerIdManager.RemoveRemotePeer(id);
                            if (_activeConnections.TryRemove(id, out var removed))
                            {
                                removed.CompleteReceive();
                            }

                            _deviceManager.RemoveDevice(id);
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

                    // MPC fires NotConnected for the departing peer before removing it from
                    // ConnectedPeers, so check whether this peer was the only remaining one
                    // while it is still present in the session's list.
                    MCSession? sessionToDisposePeer;
                    lock (_sessionLock)
                    {
                        var isLastPeer = _session is not null
                            && _session.ConnectedPeers.All(p => PeerIdManager.PeerKey(p) == id);
                        sessionToDisposePeer = isLastPeer ? _session : null;
                        if (isLastPeer)
                        {
                            _session = null;
                        }
                    }

                    PeerIdManager.RemoveRemotePeer(id);
                    _deviceManager.RemoveDevice(id);

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
            var id = PeerIdManager.PeerKey(peerID);

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
        var id = PeerIdManager.PeerKey(fromPeer);

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

        _progressObservers[resourceName] = observer;
    }

    void OnResourceFinished(
        string resourceName,
        MCPeerID fromPeer,
        NSUrl? localUrl,
        NSError? error)
    {
        try
        {
            var id = PeerIdManager.PeerKey(fromPeer);
            var loc = localUrl?.ToString() ?? "null";

            LogResourceReceiveFinished(id, fromPeer.DisplayName, resourceName, loc, error?.LocalizedDescription);

            if (_progressObservers.TryRemove(resourceName, out var observer))
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

            var destinationPath = Path.Combine(Options.ReceivedFilesDirectory, resourceName);

            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
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

    sealed class AdvertiserDelegate(NearbyDevicesImplementation nearbyConnections) : NSObject, IMCNearbyServiceAdvertiserDelegate
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

    sealed class BrowserDelegate(NearbyDevicesImplementation nearbyConnections) : NSObject, IMCNearbyServiceBrowserDelegate
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

    sealed class SessionDelegate(NearbyDevicesImplementation nearbyConnections) : NSObject, IMCSessionDelegate
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
