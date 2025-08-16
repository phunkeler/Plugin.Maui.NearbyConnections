using Foundation;
using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections;

public partial class NearbyConnectionsDiscoverer : NSObject, IMCNearbyServiceBrowserDelegate
{
    readonly NearbyConnectionsManager _connectionManager = new();

    MCNearbyServiceBrowser? _browser;
    MCPeerID? _myPeerId;

    /// <summary>
    /// Starts discovering nearby connections.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Task PlatformStartDiscovering(IDiscoveringOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[DISCOVERER] Starting discovery for service: {options.ServiceName}");

        // Get or create peer ID
        _myPeerId = _connectionManager.GetPeerId(options.ServiceName);

        if (_myPeerId is null)
        {
            Console.WriteLine("[DISCOVERER] ERROR: Failed to create or retrieve peer ID");
            throw new InvalidOperationException("Failed to create or retrieve my peer ID");
        }

        Console.WriteLine($"[DISCOVERER] Using peer ID: {_myPeerId.DisplayName}");

        Console.WriteLine("[DISCOVERER] Creating MCNearbyServiceBrowser...");
        _browser = new MCNearbyServiceBrowser(_myPeerId, options.ServiceName)
        {
            Delegate = this
        };

        Console.WriteLine("[DISCOVERER] Browser created, setting delegate and starting...");
        _browser.StartBrowsingForPeers();
        Console.WriteLine("[DISCOVERER] StartBrowsingForPeers() called successfully");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops discovering nearby connections.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task PlatformStopDiscovering(CancellationToken cancellationToken = default)
    {
        if (_browser is not null)
        {
            _browser.StopBrowsingForPeers();
            _browser.Delegate = null!;
            _browser.Dispose();
            _browser = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        // Handle found peer
        Console.WriteLine($"[DISCOVERER] 🎉 SUCCESS: Found peer: {peerID.DisplayName}");

        if (info != null && info.Count > 0)
        {
            Console.WriteLine($"[DISCOVERER] Peer info contains {info.Count} items:");
            foreach (var key in info.Keys)
            {
                var value = info[key];
                Console.WriteLine($"[DISCOVERER]   {key}: {value}");
            }
        }
        else
        {
            Console.WriteLine("[DISCOVERER] No additional peer info available");
        }
    }

    /// <inheritdoc/>
    public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        // Handle lost peer
        Console.WriteLine($"[DISCOVERER] ❌ Lost peer: {peerID.DisplayName}");
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