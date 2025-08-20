namespace Plugin.Maui.NearbyConnections.Models;

/// <summary>
/// Represents a message received from a peer.
/// </summary>
public class MessageData
{
    /// <summary>
    /// Gets or sets the ID of the peer that sent the message.
    /// </summary>
    public required string FromPeerId { get; init; }

    /// <summary>
    /// Gets or sets the display name of the peer that sent the message.
    /// </summary>
    public required string FromDisplayName { get; init; }

    /// <summary>
    /// Gets or sets the message content as a byte array.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the message was received.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the message content as a UTF-8 string.
    /// </summary>
    public string GetTextContent() => System.Text.Encoding.UTF8.GetString(Data);
}