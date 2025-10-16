using Foundation;
using MultipeerConnectivity;
using Plugin.Maui.NearbyConnections.Device;

namespace Plugin.Maui.NearbyConnections.Discover;

public partial class Discoverer : NSObject, IMCNearbyServiceBrowserDelegate
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    MCNearbyServiceBrowser? _browser;

    Task PlatformStartDiscovering(DiscoverOptions options)
    {
        var _myPeerId = _myMCPeerIDManager.GetPeerId(options.ServiceName) ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

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
    {
        // Serialize the MCPeerID to get a unique identifier
        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var key = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        var device = new NearbyDevice(key, peerID.DisplayName);
        _nearbyConnections._discoveredDevices.TryAdd(key, device);
    }

    /// <inheritdoc/>
    public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        // Serialize the MCPeerID to get a unique identifier
        using var data = _myMCPeerIDManager.ArchivePeerId(peerID);
        var key = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

        if (!_discoveredDevices.TryRemove(key, out var nearbyDeviceLost))
            return;

        Console.WriteLine($"[DISCOVERER] Lost device: {nearbyDeviceLost}");
    }

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