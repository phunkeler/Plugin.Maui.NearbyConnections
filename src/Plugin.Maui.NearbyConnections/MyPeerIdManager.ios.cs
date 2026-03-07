namespace Plugin.Maui.NearbyConnections;

static class PeerIdArchiver
{
    public static MCPeerID? UnarchivePeerId(NSData data)
    {
        var result = NSKeyedUnarchiver.GetUnarchivedObject(typeof(MCPeerID), data, out var error);

        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        if (result is MCPeerID peerId)
        {
            return peerId;
        }

        throw new InvalidOperationException("Failed to unarchive MCPeerID: Result is null or of wrong type");
    }

    public static NSData ArchivePeerId(MCPeerID peerId)
    {
        var data = NSKeyedArchiver.GetArchivedData(peerId, true, out var error);

        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        return data ?? throw new InvalidOperationException("Failed to archive MCPeerID: Result is null");
    }
}


static class PeerIdManager
{
    const string KEYPREFIX = "plugin.maui.nearbyconnections";
    const string KEY_DISPLAYNAME = KEYPREFIX + "." + "DisplayName";
    const string KEY_MCPEERID = KEYPREFIX + "." + "MCPeerId";

    public static MCPeerID? GetLocalPeerId(string displayName)
    {
        if (TryGetStoredDisplayName(displayName, out var mCPeerID))
        {
            return mCPeerID;
        }

        var peerId = new MCPeerID(displayName);
        var archivedData = PeerIdArchiver.ArchivePeerId(peerId);
        StorePeerIdData(displayName, archivedData);

        return peerId;
    }

    public static NSData ArchivePeerId(MCPeerID mCPeerID)
        => PeerIdArchiver.ArchivePeerId(mCPeerID);

    public static MCPeerID? UnarchivePeerId(NSData data)
        => PeerIdArchiver.UnarchivePeerId(data);

    static void StorePeerIdData(string displayName, NSData peerIdData)
    {
        var defaults = NSUserDefaults.StandardUserDefaults;
        defaults.SetString(displayName, KEY_DISPLAYNAME);
        defaults.SetValueForKey(peerIdData, new NSString(KEY_MCPEERID));
        defaults.Synchronize();
    }

    static bool TryGetStoredDisplayName(string displayName, [NotNullWhen(true)] out MCPeerID? mCPeerID)
    {
        mCPeerID = null;

        var storedDisplayName = NSUserDefaults
            .StandardUserDefaults
            .StringForKey(KEY_DISPLAYNAME);

        if (storedDisplayName?.Equals(displayName, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            var storedPeerId = NSUserDefaults
                .StandardUserDefaults
                .DataForKey(KEY_MCPEERID);

            if (storedPeerId is not null)
            {
                mCPeerID = PeerIdArchiver.UnarchivePeerId(storedPeerId);
                return mCPeerID is not null;
            }
        }

        return false;
    }
}