using Foundation;
using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
internal partial class Advertiser : NSObject, IMCNearbyServiceAdvertiserDelegate
{
    readonly MyPeerIdManager _myMCPeerIDManager = new();

    MCNearbyServiceAdvertiser? _advertiser;

    Task PlatformStartAdvertising(AdvertiseOptions options)
    {
        var myPeerId = _myMCPeerIDManager.GetPeerId(options.ServiceName) ?? throw new InvalidOperationException("Failed to create or retrieve my peer ID");

        NSDictionary? advertisingInfo = null;
        if (options.AdvertisingInfo?.Any() == true)
        {
            Console.WriteLine($"[ADVERTISER] Adding advertising info with {options.AdvertisingInfo.Count} items");
            advertisingInfo = NSDictionary.FromObjectsAndKeys(
                options.AdvertisingInfo.Values.ToArray(),
                options.AdvertisingInfo.Keys.ToArray()
            );
        }

        _advertiser = new MCNearbyServiceAdvertiser(
            myPeerID: myPeerId,
            info: advertisingInfo,
            serviceType: options.ServiceName
        )
        {
            Delegate = this
        };

        _advertiser.StartAdvertisingPeer();

        return Task.CompletedTask;
    }

    void PlatformStopAdvertising()
        => _advertiser?.StopAdvertisingPeer();

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
    }
}