using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Plugin.Maui.NearbyConnections;


sealed partial class PeerLookup
{
    readonly ConcurrentDictionary<string, MCPeerID> _handles = new(StringComparer.Ordinal);
    readonly ConditionalWeakTable<MCPeerID, string> _keyCache = [];

    internal required ILogger Logger { get; init; }

    public NearbyDevice Track(MCPeerID peerID)
    {
        var key = PeerKey(peerID);
        _handles[key] = peerID;
        var device = Record(key, peerID.DisplayName);

        // The sanitized name from the device, never peerID.DisplayName: the raw value is
        // remote-supplied and reaches a log sink here.
        LogTrackingRemotePeer(Logger, key, device.DisplayName);

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
            // Sanitized: this fallback makes the display name the device's Id, so an unfiltered
            // value would become a dictionary key and reach every log line that names the device.
            var fallback = Sanitize(peerID.DisplayName);

            LogFailedToDerivePeerKey(Logger, fallback, ex);

            return fallback ?? string.Empty;
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
    static partial void LogFailedToDerivePeerKey(ILogger logger, string? displayName, Exception error);

    [LoggerMessage(
        EventId = 3030,
        Level = LogLevel.Trace,
        Message = "Tracking remote peer: Key={Key}, DisplayName={DisplayName}")]
    static partial void LogTrackingRemotePeer(ILogger logger, string key, string? displayName);

    [LoggerMessage(
        EventId = 3031,
        Level = LogLevel.Trace,
        Message = "Removing remote peer: Key={Key}")]
    static partial void LogRemovingRemotePeer(ILogger logger, string key);
}
