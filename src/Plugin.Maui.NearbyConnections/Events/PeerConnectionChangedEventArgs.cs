using Plugin.Maui.NearbyConnections.Models;

namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event arguments for when a peer connection state changes.
/// </summary>
public class PeerConnectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the peer device whose connection state changed.
    /// </summary>
    public required PeerDevice Peer { get; init; }

    /// <summary>
    /// Gets the previous connection state.
    /// </summary>
    public PeerConnectionState PreviousState { get; init; }
}