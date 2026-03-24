using System.Security.Cryptography;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Manages the local device's <see cref="MCPeerID"/>, tracks discovered remote peers,
/// and provides utilities for working with peer IDs.
/// </summary>
sealed partial class PeerIdManager
{
    static readonly string s_keyPrefix = typeof(PeerIdManager).Namespace ?? "Plugin.Maui.NearbyConnections";
    static readonly string s_keyDisplayName = $"{s_keyPrefix}.{nameof(NearbyConnectionsOptions.DisplayName)}";
    static readonly string s_keyMCPeerId = $"{s_keyPrefix}.{nameof(MCPeerID)}";

    readonly ConcurrentDictionary<string, MCPeerID> _remotePeers = [];
    readonly ILogger<PeerIdManager> _logger;

    public PeerIdManager(ILogger<PeerIdManager> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Returns the persisted local <see cref="MCPeerID"/> for the given display name,
    /// or creates and persists a new one if none exists.
    /// </summary>
    public MCPeerID GetLocalPeerId(string displayName)
    {
        if (TryGetStoredPeerId(displayName, out var peerId))
        {
            return peerId;
        }

        peerId = new MCPeerID(displayName);

        try
        {
            StorePeerId(displayName, Archive(peerId));
        }
        catch (Exception ex)
        {
            LogFailedToStoreLocalPeer(displayName, ex.Message);
        }

        return peerId;
    }

    /// <summary>
    /// Derives a stable, opaque string key from a remote <see cref="MCPeerID"/>.
    /// The key is a hex-encoded truncated SHA-256 of the peer's archived bytes,
    /// which are stable for the lifetime of the peer.
    /// </summary>
    public string PeerKey(MCPeerID peerID)
    {
        if (peerID is null)
        {
            return string.Empty;
        }

        try
        {
            using var data = Archive(peerID);
            var hash = SHA256.HashData([.. data]);
            return Convert.ToHexString(hash[..8]);
        }
        catch (Exception ex)
        {
            LogFailedToDerivePeerKey(peerID.DisplayName, ex.Message);
            return peerID.DisplayName;
        }
    }

    /// <summary>
    /// Registers a remote peer, deriving its key. Returns the key.
    /// Safe to call multiple times for the same peer.
    /// </summary>
    public string TrackRemotePeer(MCPeerID peerID)
    {
        var key = PeerKey(peerID);
        _remotePeers.TryAdd(key, peerID);
        return key;
    }

    /// <summary>
    /// Tries to get the <see cref="MCPeerID"/> for a previously tracked remote peer.
    /// </summary>
    public bool TryGetRemotePeer(string key, [NotNullWhen(true)] out MCPeerID? peerID)
        => _remotePeers.TryGetValue(key, out peerID);

    /// <summary>
    /// Removes a remote peer by key. Called when a peer is lost or disconnected.
    /// </summary>
    public void RemoveRemotePeer(string key)
        => _remotePeers.TryRemove(key, out _);

    /// <summary>
    /// Removes all tracked remote peers. Called on full teardown.
    /// </summary>
    public void ClearRemotePeers()
        => _remotePeers.Clear();

    static NSData Archive(MCPeerID peerId)
    {
        var data = NSKeyedArchiver.GetArchivedData(peerId, true, out var error);

        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        return data ?? throw new InvalidOperationException("Failed to archive MCPeerID: Result is null");
    }

    static MCPeerID Unarchive(NSData data)
    {
        var result = NSKeyedUnarchiver.GetUnarchivedObject(typeof(MCPeerID), data, out var error);

        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        return result as MCPeerID
            ?? throw new InvalidOperationException("Failed to unarchive MCPeerID: Result is null or of wrong type");
    }

    static void StorePeerId(string displayName, NSData peerIdData)
    {
        var defaults = NSUserDefaults.StandardUserDefaults;
        defaults.SetString(displayName, s_keyDisplayName);
        defaults.SetValueForKey(peerIdData, new NSString(s_keyMCPeerId));
    }

    static bool TryGetStoredPeerId(string displayName, [NotNullWhen(true)] out MCPeerID? peerId)
    {
        peerId = null;

        var storedDisplayName = NSUserDefaults.StandardUserDefaults.StringForKey(s_keyDisplayName);

        if (storedDisplayName?.Equals(displayName, StringComparison.Ordinal) ?? false)
        {
            var storedData = NSUserDefaults.StandardUserDefaults.DataForKey(s_keyMCPeerId);

            if (storedData is not null)
            {
                peerId = Unarchive(storedData);
                return peerId is not null;
            }
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store local peer: DisplayName={DisplayName}, Error={Error}")]
    partial void LogFailedToStoreLocalPeer(string displayName, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to derive peer key for '{DisplayName}', falling back to DisplayName: {Error}")]
    partial void LogFailedToDerivePeerKey(string displayName, string error);
}
