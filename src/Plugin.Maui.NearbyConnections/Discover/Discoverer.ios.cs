using Foundation;
using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections.Discover;

internal partial class Discoverer : NSObject, IMCNearbyServiceBrowserDelegate
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    MCNearbyServiceBrowser? _browser;

    Task PlatformStartDiscovering(DiscoverOptions options)
    {
        var _myPeerId = _myMCPeerIDManager.GetPeerId(options.ServiceName)
            ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        _browser = new MCNearbyServiceBrowser(_myPeerId, options.ServiceName)
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
        // Handle browsing start failure
        Console.WriteLine($"[DISCOVERER] ERROR: Failed to start browsing: {error.LocalizedDescription}");
        Console.WriteLine($"[DISCOVERER] Error code: {error.Code}, Domain: {error.Domain}");

        if (error.UserInfo != null)
        {
            Console.WriteLine($"[DISCOVERER] Error details: {error.UserInfo}");
        }
    }
}