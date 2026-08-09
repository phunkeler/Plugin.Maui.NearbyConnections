namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// <see cref="MCPeerID"/>-specific operations over a <see cref="PeerRegistry{THandle}"/>,
/// deriving the registry key via a <see cref="PeerKeyProvider"/>.
/// </summary>
static partial class PeerRegistryExtensions
{
    /// <summary>
    /// Registers or re-registers a remote peer, deriving its key via <paramref name="peerKeyProvider"/>.
    /// Returns the <see cref="NearbyDevice"/> projection. Safe to call multiple times for the same peer.
    /// </summary>
    public static NearbyDevice TrackRemotePeer(
        this PeerRegistry<MCPeerID> registry,
        PeerKeyProvider peerKeyProvider,
        MCPeerID peerID,
        ILogger logger)
    {
        var key = peerKeyProvider.PeerKey(peerID);
        var device = registry.Record(key, peerID, peerID.DisplayName);
        LogTrackingRemotePeer(logger, key, peerID.DisplayName);
        return device;
    }

    /// <summary>
    /// Removes a remote peer by key, returning its <see cref="NearbyDevice"/> projection, or
    /// <see langword="null"/> if it was not tracked. Called when a peer is lost or disconnected.
    /// </summary>
    public static NearbyDevice? RemoveRemotePeer(this PeerRegistry<MCPeerID> registry, string key, ILogger logger)
    {
        LogRemovingRemotePeer(logger, key);
        return registry.Remove(key);
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Tracking remote peer: Key={Key}, DisplayName={DisplayName}")]
    static partial void LogTrackingRemotePeer(ILogger logger, string key, string displayName);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Removing remote peer: Key={Key}")]
    static partial void LogRemovingRemotePeer(ILogger logger, string key);
}
