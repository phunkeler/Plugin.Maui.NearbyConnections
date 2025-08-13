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
        // Get or create peer ID
        _myPeerId = _connectionManager.GetPeerId(options.ServiceName);

        if (_myPeerId is null)
        {
            throw new InvalidOperationException("Failed to create or retrieve peer ID");
        }

        // Convert discovery info to NSDictionary
        NSDictionary? discoveryInfo = null;
        if (options.DiscoveryInfo?.Any() == true)
        {
            discoveryInfo = NSDictionary.FromObjectsAndKeys(
                options.DiscoveryInfo.Values.ToArray(),
                options.DiscoveryInfo.Keys.ToArray()
            );
        }

        // Create advertiser
        _advertiser = new MCNearbyServiceAdvertiser(
            myPeerID: _myPeerId,
            info: discoveryInfo,
            serviceType: options.ServiceName
        )
        {
            Delegate = this
        };

        // Start advertising
        _advertiser.StartAdvertisingPeer();

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

    /// <summary>
    /// Called when the platform is disposing.
    /// </summary>
    public void PlatformIsDisposing()
    {

        if (_advertiser != null)
        {
            _advertiser.StopAdvertisingPeer();
            _advertiser.Delegate = null!;
            _advertiser.Dispose();
            _advertiser = null;
        }
        base.Dispose(true);
    }

    /// <inheritdoc/>
    public void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
    {
        // Handle advertising start failure
        // This would typically trigger an exception in StartAdvertisingCore
        System.Diagnostics.Debug.WriteLine($"Failed to start advertising: {error.LocalizedDescription}");
    }

    /// <inheritdoc/>
    public void DidReceiveInvitationFromPeer(
        MCNearbyServiceAdvertiser advertiser,
        MCPeerID peerID,
        NSData? context,
        MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
    {
        // Handle incoming connection invitations
        // This is where you'd fire events to notify the application
        // For now, auto-reject invitations (implement proper handling as needed)
        System.Diagnostics.Debug.WriteLine($"Received invitation from: {peerID.DisplayName}");

        // You would typically:
        // 1. Parse context data
        // 2. Fire an event to let the app decide
        // 3. Create MCSession if accepting
        // 4. Call invitationHandler with decision

        invitationHandler(false, null); // Auto-reject for now
    }
}