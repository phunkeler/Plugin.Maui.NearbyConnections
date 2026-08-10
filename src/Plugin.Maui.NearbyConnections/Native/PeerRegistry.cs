namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The translation layer's own bookkeeping for remote peers, keyed by the platform-neutral id that
/// becomes <see cref="NearbyDevice.Id"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not <see cref="NearbyDeviceRegistry"/>.</b> That one is the session's device set — the store
/// behind <c>INearby.Devices</c>, which publishes changes and owns status transitions. This one
/// lives below the platform boundary and exists so a native callback can turn a peer handle back
/// into the <see cref="NearbyDevice"/> it already minted for it. The two never talk; the session
/// projects this layer's events into that one.
/// </para>
/// <para>
/// <c>Peer</c> rather than <c>Device</c> is deliberate and permitted: this is internal code in
/// <c>Native/</c>, where the platform's own vocabulary is precise, and the name keeps it distinct
/// from the public device registry. See <c>.claude/rules/naming.md</c>, which cites this type by
/// name.
/// </para>
/// <para>
/// This file holds everything that is keyed by device id, which is every operation the session
/// performs on both platforms. iOS additionally needs the native <c>MCPeerID</c> behind each key
/// for its <c>MCSession</c> calls, and <c>PeerRegistry.ios.cs</c> adds that — a second dictionary
/// and the entry points that populate it. Android allocates none of it: its endpoint id is already
/// the native handle, so the key <em>is</em> the handle.
/// </para>
/// <para>
/// The shared half is deliberately free of any platform-specific type reference, so it compiles and
/// unit tests on every target framework including plain <c>net10.0</c>.
/// </para>
/// </remarks>
sealed partial class PeerRegistry
{
    readonly ConcurrentDictionary<string, NearbyDevice> _peers = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a peer under <paramref name="key"/> if absent, returning its
    /// <see cref="NearbyDevice"/> projection. Safe to call repeatedly for the same key; an existing
    /// entry is left as-is rather than replaced, so a rediscovery cannot reset a device that has
    /// since connected.
    /// </summary>
    public NearbyDevice Record(string key, string? displayName)
        => _peers.GetOrAdd(key, static (k, name) => new NearbyDevice(k, name), displayName);

    /// <summary>
    /// Tries to get the <see cref="NearbyDevice"/> previously registered under <paramref name="key"/>.
    /// </summary>
    public bool TryGetDevice(string key, [NotNullWhen(true)] out NearbyDevice? device)
        => _peers.TryGetValue(key, out device);

    /// <summary>
    /// Removes the peer registered under <paramref name="key"/>, returning its
    /// <see cref="NearbyDevice"/> projection, or <see langword="null"/> if none was registered.
    /// </summary>
    public NearbyDevice? Remove(string key)
    {
        PlatformRemove(key);

        return _peers.TryRemove(key, out var device)
            ? device
            : null;
    }

    /// <summary>
    /// Removes all tracked peers.
    /// </summary>
    public void Clear()
    {
        PlatformClear();
        _peers.Clear();
    }

    /// <summary>
    /// Drops the native handle held alongside <paramref name="key"/>, where the platform keeps one.
    /// No-op where the key is itself the handle.
    /// </summary>
    partial void PlatformRemove(string key);

    /// <summary>
    /// Drops every native handle, where the platform keeps them. No-op otherwise.
    /// </summary>
    partial void PlatformClear();
}
