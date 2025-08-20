namespace Plugin.Maui.NearbyConnections.Models;

/// <summary>
/// Represents a discovered nearby peer device.
/// </summary>
public class PeerDevice
{
    /// <summary>
    /// Gets or sets the unique identifier for the peer.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the display name of the peer.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the current connection state of the peer.
    /// </summary>
    public PeerConnectionState ConnectionState { get; set; } = PeerConnectionState.NotConnected;

    /// <summary>
    /// Gets or sets additional context information for the peer.
    /// </summary>
    public string? Context { get; init; }
}