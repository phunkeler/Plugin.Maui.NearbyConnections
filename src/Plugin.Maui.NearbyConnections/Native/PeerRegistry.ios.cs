using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Plugin.Maui.NearbyConnections;


sealed partial class PeerRegistry
{
    readonly ConcurrentDictionary<string, MCPeerID> _handles = new(StringComparer.Ordinal);
    readonly ConditionalWeakTable<MCPeerID, string> _keyCache = [];
    readonly Lock _localPeerIdLock = new();

    MCPeerID? _localPeerId;

    internal required ILogger Logger { get; init; }

    public NearbyDevice Track(MCPeerID peerID)
    {
        var key = PeerKey(peerID);
        _handles[key] = peerID;
        var device = Record(key, peerID.DisplayName);

        LogTrackingRemotePeer(Logger, key, peerID.DisplayName);

        return device;
    }

    public bool TryGetHandle(string key, [NotNullWhen(true)] out MCPeerID? handle)
        => _handles.TryGetValue(key, out handle);

    public string PeerKey(MCPeerID peerID)
    {
        if (peerID is null)
        {
            return string.Empty;
        }

        if (_keyCache.TryGetValue(peerID, out var cached))
        {
            return cached;
        }

        try
        {
            var archived = NSKeyedArchiver.GetArchivedData(peerID, true, out var error);

            if (error is not null)
            {
                throw new NSErrorException(error);
            }

            using var data = archived
                ?? throw new InvalidOperationException("Failed to archive MCPeerID: Result is null");

#pragma warning disable IDE0305 // Simplify collection initialization
            var hash = SHA256.HashData(data.ToArray());
#pragma warning restore IDE0305
            var key = Convert.ToHexString(hash[..8]);
            _keyCache.AddOrUpdate(peerID, key);
            return key;
        }
        catch (Exception ex)
        {
            LogFailedToDerivePeerKey(Logger, peerID.DisplayName, ex);
            return peerID.DisplayName;
        }
    }

    public MCPeerID GetLocalPeerId(string displayName)
    {
        if (_localPeerId is not null)
        {
            return _localPeerId;
        }

        lock (_localPeerIdLock)
        {
            var created = _localPeerId is null;
            _localPeerId ??= new MCPeerID(displayName);

            if (created)
            {
                LogCreatedLocalPeer(Logger, displayName);
            }

            return _localPeerId;
        }
    }

    partial void PlatformRemove(string key)
    {
        LogRemovingRemotePeer(Logger, key);
        _handles.TryRemove(key, out _);
    }

    partial void PlatformClear()
        => _handles.Clear();

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Error,
        Message = "Failed to derive peer key for '{DisplayName}', falling back to DisplayName.")]
    static partial void LogFailedToDerivePeerKey(ILogger logger, string displayName, Exception error);

    [LoggerMessage(
        EventId = 3020,
        Level = LogLevel.Debug,
        Message = "Created local peer: DisplayName={DisplayName}")]
    static partial void LogCreatedLocalPeer(ILogger logger, string displayName);

    [LoggerMessage(
        EventId = 3030,
        Level = LogLevel.Trace,
        Message = "Tracking remote peer: Key={Key}, DisplayName={DisplayName}")]
    static partial void LogTrackingRemotePeer(ILogger logger, string key, string displayName);

    [LoggerMessage(
        EventId = 3031,
        Level = LogLevel.Trace,
        Message = "Removing remote peer: Key={Key}")]
    static partial void LogRemovingRemotePeer(ILogger logger, string key);
}
