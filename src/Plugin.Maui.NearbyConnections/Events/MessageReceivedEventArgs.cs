using Plugin.Maui.NearbyConnections.Models;

namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event arguments for when a message is received from a peer.
/// </summary>
public class PeerMessageReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the received message data.
    /// </summary>
    public required MessageData Message { get; init; }
}