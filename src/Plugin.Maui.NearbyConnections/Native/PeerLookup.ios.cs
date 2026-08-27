using System.Runtime.CompilerServices;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PeerLookup
{
    readonly ConcurrentDictionary<string, MCPeerID> _handles = new(StringComparer.Ordinal);

    /// <summary>
    /// The device id minted for each <see cref="MCPeerID"/> this session has seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="ConditionalWeakTable{TKey, TValue}"/> keyed on the peer object, because
    /// MultipeerConnectivity hands the same instance back on every callback for a peer and treats
    /// two identically-named instances as different peers. Object identity is therefore the
    /// platform's own notion of "same peer", and using it directly is both exact and free.
    /// </para>
    /// <para>
    /// This replaced hashing the archived peer. That derivation had to be salted to stop the id
    /// being a reversible pseudonym of the display name — the archive contains the name — and it
    /// needed a fallback for archive failure, which once returned the name itself as the id. A
    /// minted random id has neither problem by construction, and matches what Android does.
    /// </para>
    /// </remarks>
    readonly ConditionalWeakTable<MCPeerID, string> _deviceIds = [];

    internal required ILogger Logger { get; init; }

    public NearbyDevice Track(MCPeerID peerID)
    {
        var key = DeviceIdFor(peerID);
        _handles[key] = peerID;
        var device = Record(key, peerID.DisplayName);

        LogTrackingRemotePeer(Logger, key);

        return device;
    }

    public bool TryGetHandle(string deviceId, [NotNullWhen(true)] out MCPeerID? handle)
        => _handles.TryGetValue(deviceId, out handle);

    /// <summary>
    /// The device id for a Multipeer Connectivity peer, minted on first sight.
    /// </summary>
    /// <remarks>
    /// The iOS half of the identity contract: it turns a platform handle into the same kind of
    /// opaque, session-scoped, randomly minted id Android produces, so
    /// <see cref="NearbyDevice.Id"/> means exactly one thing on both platforms. The mapping is kept
    /// against the peer object rather than against anything the peer supplied — see
    /// <see cref="_deviceIds"/>.
    /// </remarks>
    public string DeviceIdFor(MCPeerID peerID)
    {
        if (peerID is null)
        {
            return string.Empty;
        }

        if (_deviceIds.TryGetValue(peerID, out var existing))
        {
            return existing;
        }

        // The weak table is this platform's native-to-id map: an MCPeerID has no stable string form
        // to key it by, so peer object identity is the mapping.
        var deviceId = MintDeviceId();

        _deviceIds.AddOrUpdate(peerID, deviceId);

        return deviceId;
    }

    partial void PlatformRemove(string deviceId)
    {
        LogRemovingRemotePeer(Logger, deviceId);

        if (_handles.TryRemove(deviceId, out var handle))
        {
            handle.Dispose();
        }
    }

    partial void PlatformClear()
    {
        foreach (var (key, _) in _handles)
        {
            if (_handles.TryRemove(key, out var handle))
            {
                handle.Dispose();
            }
        }

        _handles.Clear();
    }

    [LoggerMessage(
        EventId = 3030,
        Level = LogLevel.Trace,
        Message = "Tracking remote peer: DeviceId={DeviceId}")]
    static partial void LogTrackingRemotePeer(ILogger logger, string deviceId);

    [LoggerMessage(
        EventId = 3031,
        Level = LogLevel.Trace,
        Message = "Removing remote peer: DeviceId={DeviceId}")]
    static partial void LogRemovingRemotePeer(ILogger logger, string deviceId);
}
