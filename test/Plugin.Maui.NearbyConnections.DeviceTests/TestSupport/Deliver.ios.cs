namespace Plugin.Maui.NearbyConnections.DeviceTests;

/// <summary>
/// Drives the inbound data callback the way MPC does, owning the <see cref="NSData"/> lifetime so
/// tests read as "this peer sent this" rather than as native buffer construction.
/// </summary>
static class Deliver
{
    /// <summary>Delivers a plugin control frame from <paramref name="peerId"/>.</summary>
    /// <param name="platform">The platform whose callback receives the frame.</param>
    /// <param name="peerId">The peer the frame arrives from.</param>
    /// <param name="type">The control type to encode.</param>
    public static void ControlFrame(PlatformNearby platform, MCPeerID peerId, ControlMessageType type)
        => Bytes(platform, peerId, ControlMessage.Encode(type));

    /// <summary>
    /// Delivers a frame carrying a valid signature but an undefined control type, which a peer on a
    /// newer plugin version would send.
    /// </summary>
    /// <param name="platform">The platform whose callback receives the frame.</param>
    /// <param name="peerId">The peer the frame arrives from.</param>
    public static void UnknownControlFrame(PlatformNearby platform, MCPeerID peerId)
    {
        var frame = ControlMessage.Encode(ControlMessageType.Disconnect);
        frame[^1] = 0xFF;

        Bytes(platform, peerId, frame);
    }

    /// <summary>Delivers raw application bytes from <paramref name="peerId"/>.</summary>
    /// <param name="platform">The platform whose callback receives the data.</param>
    /// <param name="peerId">The peer the data arrives from.</param>
    /// <param name="data">The bytes to deliver.</param>
    public static void Bytes(PlatformNearby platform, MCPeerID peerId, byte[] data)
    {
        using var native = NSData.FromArray(data);

        platform.OnDataReceived(native, peerId);
    }
}
