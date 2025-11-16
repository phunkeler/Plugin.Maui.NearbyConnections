namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer : NSObject, IMCNearbyServiceBrowserDelegate
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    MCNearbyServiceBrowser? _browser;

    Task PlatformStartDiscovering(DiscoverOptions options)
    {
        var myPeerId = _myMCPeerIDManager.GetPeerId(options.ServiceName)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        _browser = new MCNearbyServiceBrowser(
            myPeerID: myPeerId,
            serviceType: options.ServiceName)
        {
            Delegate = this
        };

        _browser.StartBrowsingForPeers();

        return Task.CompletedTask;
    }

    void PlatformStopDiscovering()
        => _browser?.StopBrowsingForPeers();

    /// <inheritdoc/>
    public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
        => _nearbyConnections.FoundPeer(browser, peerID, info);

    /// <inheritdoc/>
    public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
        => _nearbyConnections.LostPeer(browser, peerID);

    /// <inheritdoc/>
    public void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
    {
        if (error.UserInfo != null)
        {
            Console.WriteLine($"[DISCOVERER] Error details: {error.UserInfo}");
        }
    }
}