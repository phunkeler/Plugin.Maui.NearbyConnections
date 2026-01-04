namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer : NSObject, IMCNearbyServiceBrowserDelegate
{
    MCNearbyServiceBrowser? _browser;

    Task PlatformStartDiscovering()
    {
        var options = _nearbyConnections.Options;

        var myPeerId = _nearbyConnections.MyMCPeerIDManager.GetPeerId(options.ServiceName)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        _browser = new MCNearbyServiceBrowser(
            myPeerID: myPeerId,
            serviceType: options.ServiceName)
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
        if (error.UserInfo != null)
        {
            Console.WriteLine($"[DISCOVERER] Error details: {error.UserInfo}");
        }
    }
}