namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The iOS half of <see cref="PeerRegistry"/>: the native <see cref="MCPeerID"/> behind each device
/// key, which every <c>MCSession</c> call needs and which Android has no equivalent of.
/// </summary>
/// <remarks>
/// A second dictionary rather than a wider value type on the shared one, so Android — where the
/// endpoint id is already the native handle — allocates nothing for a slot it would only ever fill
/// with a copy of the key. The two are written under the same public operations, so they cannot
/// drift: <see cref="PeerRegistry.Remove"/> and <see cref="PeerRegistry.Clear"/> call the partials
/// implemented here.
/// </remarks>
sealed partial class PeerRegistry
{
    readonly ConcurrentDictionary<string, MCPeerID> _handles = new(StringComparer.Ordinal);

    /// <summary>
    /// The provider that derives a device key from an <see cref="MCPeerID"/>. Required on iOS.
    /// </summary>
    internal required PeerKeyProvider PeerKeyProvider { get; init; }

    /// <summary>
    /// Required on iOS, where tracking and removal are traced.
    /// </summary>
    internal required ILogger Logger { get; init; }

    /// <summary>
    /// Registers or re-registers a remote peer, deriving its key from <paramref name="peerID"/> and
    /// storing the handle alongside the device. Returns the <see cref="NearbyDevice"/> projection.
    /// Safe to call multiple times for the same peer.
    /// </summary>
    public NearbyDevice Track(MCPeerID peerID)
    {
        var key = PeerKeyProvider.PeerKey(peerID);

        // The handle is written before the device so a concurrent TryGetHandle for a key that
        // Record has just published cannot miss it.
        _handles[key] = peerID;

        var device = Record(key, peerID.DisplayName);

        LogTrackingRemotePeer(Logger, key, peerID.DisplayName);

        return device;
    }

    /// <summary>
    /// Tries to get the native <see cref="MCPeerID"/> registered under <paramref name="key"/>.
    /// </summary>
    public bool TryGetHandle(string key, [NotNullWhen(true)] out MCPeerID? handle)
        => _handles.TryGetValue(key, out handle);

    partial void PlatformRemove(string key)
    {
        LogRemovingRemotePeer(Logger, key);
        _handles.TryRemove(key, out _);
    }

    partial void PlatformClear()
        => _handles.Clear();

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
