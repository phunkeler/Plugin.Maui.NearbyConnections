namespace Plugin.Maui.NearbyConnections.Models;

/// <summary>
/// Represents the connection state of a peer device.
/// </summary>
public enum PeerConnectionState
{
    /// <summary>
    /// The peer is not connected.
    /// </summary>
    NotConnected,

    /// <summary>
    /// Connection to the peer is in progress.
    /// </summary>
    Connecting,

    /// <summary>
    /// The peer is connected and ready for communication.
    /// </summary>
    Connected,

    /// <summary>
    /// The peer connection is being disconnected.
    /// </summary>
    Disconnecting
}