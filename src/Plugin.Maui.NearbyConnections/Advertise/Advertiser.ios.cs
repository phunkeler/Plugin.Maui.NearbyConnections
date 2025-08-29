using Foundation;
using MultipeerConnectivity;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
public partial class Advertiser : NSObject, IMCNearbyServiceAdvertiserDelegate
{
    readonly NearbyConnectionsManager _connectionManager = new();

    MCNearbyServiceAdvertiser? _advertiser;
    string? _serviceName;

    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task PlatformStartAdvertising(AdvertiseOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ADVERTISER] Starting advertising with service: {options.ServiceName}");

        _serviceName = options.ServiceName;

        // Get or create peer ID
        var myPeerId = _connectionManager.GetPeerId(options.ServiceName);

        if (myPeerId is null)
        {
            Console.WriteLine("[ADVERTISER] ERROR: Failed to create or retrieve peer ID");
            throw new InvalidOperationException("Failed to create or retrieve peer ID");
        }

        Console.WriteLine($"[ADVERTISER] Using peer ID: {myPeerId.DisplayName}");

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
            myPeerID: myPeerId,
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
        if (_advertiser is not null)
        {
            _advertiser.StopAdvertisingPeer();
            _advertiser.Delegate = null!;
            _advertiser.Dispose();
            _advertiser = null;
        }
        _serviceName = null;
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

        // Transform

        // Publish
        _eventProducer.PublishAsync(new InvitationReceived
        {
            ConnectionEndpoint = peerID.DisplayName,
            InvitingPeer = new Models.PeerDevice
            {
                Id = peerID.DisplayName,
                DisplayName = peerID.DisplayName,
            },
        });
    }

    /// <summary>
    /// Disposes of resources used by the advertiser.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_advertiser is not null)
            {
                _advertiser.StopAdvertisingPeer();
                _advertiser.Delegate = null!;
                _advertiser.Dispose();
                _advertiser = null;
            }

            _connectionManager?.Dispose();
        }

        base.Dispose(disposing);
    }
}