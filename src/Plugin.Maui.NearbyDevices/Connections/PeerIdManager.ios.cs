using System.Security.Cryptography;

namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Manages the local device's <see cref="MCPeerID"/>, tracks discovered remote peers,
/// and provides utilities for working with peer IDs.
/// </summary>
sealed partial class PeerIdManager
{
    static readonly string s_keyPrefix = typeof(PeerIdManager).Namespace ?? "Plugin.Maui.NearbyDevices";
    static readonly string s_keyDisplayName = $"{s_keyPrefix}.{nameof(NearbyDevicesOptions.DisplayName)}";
    static readonly string s_keyMCPeerId = $"{s_keyPrefix}.{nameof(MCPeerID)}";

    readonly ConcurrentDictionary<string, MCPeerID> _remotePeers = [];
    readonly ILogger<PeerIdManager> _logger;

    MCPeerID? _localPeerId;
    readonly Lock _localPeerIdLock = new();

    public PeerIdManager(ILogger<PeerIdManager> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Returns the process's single canonical local <see cref="MCPeerID"/>, memoized after
    /// its first successful resolution. The first call either loads a previously persisted
    /// peer ID for <paramref name="displayName"/> or creates and persists a new one; the
    /// resulting instance is cached for the lifetime of this <see cref="PeerIdManager"/> and
    /// returned as-is by every subsequent call, regardless of the <paramref name="displayName"/>
    /// argument passed to those later calls. This is safe because
    /// <see cref="NearbyDevicesOptions.DisplayName"/> is one-time startup configuration
    /// that cannot change after initialization (see that property's doc comment), so within
    /// one process every caller already passes the same value.
    /// </summary>
    /// <remarks>
    /// Memoization also closes a native-interop lifetime hazard: without a durable managed
    /// reference, a freshly-returned <see cref="MCPeerID"/> wrapper could be collected by the
    /// GC before .NET-for-iOS's toggle-ref mechanism promotes it to a strong root, even while
    /// native code (e.g. <see cref="MCNearbyServiceAdvertiser"/>) still depends on it.
    /// </remarks>
    public MCPeerID GetLocalPeerId(string displayName)
    {
        if (_localPeerId is not null)
        {
            return _localPeerId;
        }

        lock (_localPeerIdLock)
        {
            if (_localPeerId is not null)
            {
                return _localPeerId;
            }

            if (TryGetStoredPeerId(displayName, out var storedPeerId))
            {
                LogLoadedLocalPeer(displayName);
                _localPeerId = storedPeerId;
                return _localPeerId;
            }

            var peerId = new MCPeerID(displayName);

            try
            {
                StorePeerId(displayName, Archive(peerId));
            }
            catch (Exception ex)
            {
                LogFailedToStoreLocalPeer(displayName, ex.Message);
            }

            LogCreatedLocalPeer(displayName);
            _localPeerId = peerId;
            return _localPeerId;
        }
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
        LogTrackingRemotePeer(key, peerID.DisplayName);
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
    {
        LogRemovingRemotePeer(key);
        _remotePeers.TryRemove(key, out _);
    }

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded persisted local peer: DisplayName={DisplayName}")]
    partial void LogLoadedLocalPeer(string displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created new local peer: DisplayName={DisplayName}")]
    partial void LogCreatedLocalPeer(string displayName);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Tracking remote peer: Key={Key}, DisplayName={DisplayName}")]
    partial void LogTrackingRemotePeer(string key, string displayName);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Removing remote peer: Key={Key}")]
    partial void LogRemovingRemotePeer(string key);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store local peer: DisplayName={DisplayName}, Error={Error}")]
    partial void LogFailedToStoreLocalPeer(string displayName, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to derive peer key for '{DisplayName}', falling back to DisplayName: {Error}")]
    partial void LogFailedToDerivePeerKey(string displayName, string error);
}
