namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    #region Discovery

    internal void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        _logger.FoundPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        // Deserialize advertisement from discovery info
        var advertisement = NearbyAdvertisement.FromNSDictionary(info);

        var device = new NearbyDevice(id, peerID.DisplayName, NearbyDeviceStatus.Discovered);

        if (_devices.TryAdd(id, device))
        {
            _events.OnDeviceFound(device);
        }
    }

    internal void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        _logger.LostPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        if (_devices.TryRemove(id, out var device))
        {
            _events.OnDeviceLost(device);
        }
    }

    #endregion Discovery

    #region Advertising

    internal void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        _logger.DidNotStartAdvertisingPeer(
            advertiser.ServiceType,
            advertiser.MyPeerID.DisplayName,
            error.LocalizedDescription);
    }

    internal void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        _logger.DidReceiveInvitationFromPeer(peerID.DisplayName);

        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var id = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        // Get or create device with Invited status
        if (!_devices.TryGetValue(id, out var device))
        {
            device = new NearbyDevice(
                id,
                peerID.DisplayName,
                NearbyDeviceStatus.Invited);

            _devices.TryAdd(id, device);
        }
        else
        {
            device.Status = NearbyDeviceStatus.Invited;
        }

        _events.OnConnectionRequested(device);
    }

    #endregion Advertising

    Task PlatformSendInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    Task PlatformAcceptInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    Task PlatformDeclineInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}