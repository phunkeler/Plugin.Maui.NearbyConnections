namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// <see cref="NSKeyedArchiver"/> serialization of <see cref="MCPeerID"/>, used by
/// <see cref="PeerKeyProvider"/> to derive a stable string key from a peer's archived bytes.
/// </summary>
static class PeerIdArchive
{
    public static NSData Archive(MCPeerID peerId)
    {
        var data = NSKeyedArchiver.GetArchivedData(peerId, true, out var error);

        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        return data ?? throw new InvalidOperationException("Failed to archive MCPeerID: Result is null");
    }
}
