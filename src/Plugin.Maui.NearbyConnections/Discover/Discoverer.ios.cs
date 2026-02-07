namespace Plugin.Maui.NearbyConnections.Discover;

sealed partial class Discoverer : NSObject, IMCNearbyServiceBrowserDelegate
{
    MCNearbyServiceBrowser? _browser;

    Task PlatformStartDiscovering()
    {
        var options = _nearbyConnections.Options;

        var myPeerId = _nearbyConnections.MyMCPeerIDManager.GetPeerId(options.ServiceId)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        _browser = new MCNearbyServiceBrowser(
            myPeerID: myPeerId,
            serviceType: options.ServiceId)
        {
            Delegate = this
        };

        _browser.StartBrowsingForPeers();
        IsDiscovering = true;

        return Task.CompletedTask;
    }

    void PlatformStopDiscovering()
    {
        _browser?.StopBrowsingForPeers();
        _browser?.Dispose();
        _browser = null;
        IsDiscovering = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                _browser?.StopBrowsingForPeers();
                _browser?.Dispose();
                _browser = null;
                IsDiscovering = false;
            }
        }

        base.Dispose(disposing);
    }

    public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
        => _nearbyConnections.FoundPeer(browser, peerID, info);

    public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
        => _nearbyConnections.LostPeer(browser, peerID);

    public void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
    {
        OnDiscoveryFailed();

        _nearbyConnections.Events.OnError(
            "Discovery",
            error.LocalizedDescription,
            _nearbyConnections.TimeProvider.GetUtcNow());
    }

    /// <summary>
    /// Invites a peer to join the specified session.
    /// </summary>
    /// <param name="peerID">The peer to invite.</param>
    /// <param name="session">The session to join.</param>
    /// <param name="context">Optional context data to send with the invitation.</param>
    /// <param name="timeout">The timeout in seconds for the invitation.</param>
    public void InvitePeer(MCPeerID peerID, MCSession session, NSData? context, double timeout)
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Cannot invite peer: browser is not active. Start discovery first.");
        }

        _browser.InvitePeer(peerID, session, context, timeout);
    }
}