using Plugin.Maui.NearbyConnections.Models;

namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event arguments for when a peer is discovered.
/// </summary>
public class PeerDiscoveredEventArgs : EventArgs
{
    /// <summary>
    /// Gets the discovered peer device.
    /// </summary>
    public required PeerDevice Peer { get; init; }
}