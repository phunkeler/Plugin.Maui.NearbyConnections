using Foundation;
using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
public partial class NearbyConnectionsAdvertiser : NSObject, IMCNearbyServiceAdvertiserDelegate
{
    readonly NearbyConnectionsManager _connectionManager = new();

    MCNearbyServiceAdvertiser? _advertiser;
    MCPeerID? _myPeerId;

    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task PlatformStartAdvertising(IAdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ADVERTISER] Starting advertising with service: {options.ServiceName}");
        
        // Get or create peer ID
        _myPeerId = _connectionManager.GetPeerId(options.ServiceName);

        if (_myPeerId is null)
        {
            Console.WriteLine("[ADVERTISER] ERROR: Failed to create or retrieve peer ID");
            throw new InvalidOperationException("Failed to create or retrieve peer ID");
        }

        Console.WriteLine($"[ADVERTISER] Using peer ID: {_myPeerId.DisplayName}");

        // Convert advertising info to NSDictionary
        NSDictionary? advertisingInfo = null;
        if (options.AdvertisingInfo?.Any() == true)
        {
            Console.WriteLine($"[ADVERTISER] Adding advertising info with {options.AdvertisingInfo.Count} items");
            advertisingInfo = NSDictionary.FromObjectsAndKeys(
                options.AdvertisingInfo.Values.ToArray(),
                options.AdvertisingInfo.Keys.ToArray()
            );
        }
        else
        {
            Console.WriteLine("[ADVERTISER] No advertising info provided");
        }

        // Create advertiser
        Console.WriteLine("[ADVERTISER] Creating MCNearbyServiceAdvertiser...");
        _advertiser = new MCNearbyServiceAdvertiser(
            myPeerID: _myPeerId,
            info: advertisingInfo,
            serviceType: options.ServiceName
        )
        {
            Delegate = this
        };

        Console.WriteLine("[ADVERTISER] Advertiser created, setting delegate and starting...");
        
        // Start advertising
        _advertiser.StartAdvertisingPeer();
        Console.WriteLine("[ADVERTISER] StartAdvertisingPeer() called successfully");

        // MCNearbyServiceAdvertiser.StartAdvertisingPeer() is synchronous
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops advertising with the specified options.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PlatformStopAdvertising(CancellationToken cancellationToken)
    {
        if (_advertiser != null)
        {
            _advertiser.StopAdvertisingPeer();
            _advertiser.Delegate = null!;
            _advertiser.Dispose();
            _advertiser = null;
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        // Handle advertising start failure
        Console.WriteLine($"[ADVERTISER] ERROR: Failed to start advertising: {error.LocalizedDescription}");
        Console.WriteLine($"[ADVERTISER] Error code: {error.Code}, Domain: {error.Domain}");
        
        if (error.UserInfo != null)
        {
            Console.WriteLine($"[ADVERTISER] Error details: {error.UserInfo}");
        }
    }

    /// <inheritdoc/>
    public void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        // Handle incoming connection invitations
        Console.WriteLine($"[ADVERTISER] 🎉 SUCCESS: Received invitation from peer: {peerID.DisplayName}");
        
        if (context != null && context.Length > 0)
        {
            var contextString = Foundation.NSString.FromData(context, Foundation.NSStringEncoding.UTF8);
            Console.WriteLine($"[ADVERTISER] Invitation context: {contextString}");
        }
        else
        {
            Console.WriteLine("[ADVERTISER] No context data in invitation");
        }

        // You would typically:
        // 1. Parse context data
        // 2. Fire an event to let the app decide
        // 3. Create MCSession if accepting
        // 4. Call invitationHandler with decision

        Console.WriteLine("[ADVERTISER] Auto-rejecting invitation (implement proper handling as needed)");
        invitationHandler(false, null); // Auto-reject for now
    }
}