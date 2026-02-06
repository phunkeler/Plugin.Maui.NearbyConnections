namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
    const double DefaultInvitationTimeout = 30.0;

    internal MyPeerIdManager MyMCPeerIDManager { get; } = new();

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

    internal void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        using var data = MyMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        var device = _deviceManager.SetState(id, NearbyDeviceState.ConnectionRequestedInbound)
            ?? _deviceManager.GetOrAddDevice(id, peerID.DisplayName, NearbyDeviceState.ConnectionRequestedInbound);

        Trace.WriteLine($"Received invitation from peer: Id={id}, DisplayName={peerID.DisplayName}");
        Events.OnConnectionRequested(device, TimeProvider.GetUtcNow());
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
            var myPeerId = MyMCPeerIDManager.GetPeerId(Options.ServiceId)
                ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

            _session = new MCSession(myPeerId)
            {
                Delegate = new SessionDelegate(this)
            };
        }

        _deviceManager.SetState(device.Id, NearbyDeviceState.ConnectionRequestedOutbound);

        Trace.WriteLine($"Inviting peer: Id={device.Id}, DisplayName={peerID.DisplayName}");
        _discoverer.InvitePeer(peerID, _session, context: null, DefaultInvitationTimeout);

        return Task.CompletedTask;
    }

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        throw new NotImplementedException();
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

    static void OnDataReceived(NSData data, MCPeerID peerID)
    {
        Trace.WriteLine($"Data received from peer: DisplayName={peerID.DisplayName}, Length={data.Length}");
        // TODO: Implement payload handling
    }

    #endregion Session Callbacks

    sealed class SessionDelegate(NearbyConnectionsImplementation nearbyConnections) : NSObject, IMCSessionDelegate
    {
        readonly NearbyConnectionsImplementation _nearbyConnections = nearbyConnections;

        public void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
            => _nearbyConnections.OnPeerStateChanged(peerID, state);

        public void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            => OnDataReceived(data, peerID);

        public void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
        {
            Trace.WriteLine($"Started receiving resource: {resourceName} from {fromPeer.DisplayName}");
        }

        public void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
        {
            Trace.WriteLine($"Finished receiving resource: {resourceName} from {fromPeer.DisplayName}, Error: {error?.LocalizedDescription ?? "None"}");
        }

        public void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerID)
        {
            Trace.WriteLine($"Received stream: {streamName} from {peerID.DisplayName}");
        }
    }
}