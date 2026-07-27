namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Thread-safe key/value bookkeeping for tracking discovered/connected remote devices: associates
/// a platform-neutral string key with both a native peer handle and its <see cref="NearbyDevice"/>
/// projection, so both can be looked up or removed together under a single key.
/// </summary>
/// <typeparam name="THandle">
/// The platform-specific native peer handle type. On iOS this is <c>MCPeerID</c> — a distinct
/// native object needed for <c>MCSession</c> calls. On Android, endpoint IDs are already the
/// native identifier, so Android uses <see cref="string"/> as its own handle.
/// </typeparam>
/// <remarks>
/// Deliberately free of any platform-specific type reference so it can be unit tested on every
/// target framework, including plain <c>net10.0</c>, without constructing a native handle.
/// </remarks>
sealed class PeerRegistry<THandle> where THandle : class
{
    readonly ConcurrentDictionary<string, (THandle Handle, NearbyDevice Device)> _peers = [];

    /// <summary>
    /// Registers or re-registers a peer under <paramref name="key"/>, returning its
    /// <see cref="NearbyDevice"/> projection. Safe to call multiple times for the same key;
    /// an existing entry's device is left as-is rather than replaced.
    /// </summary>
    public NearbyDevice Record(string key, THandle handle, string? displayName)
        => _peers.AddOrUpdate(
            key,
            _ => (handle, new NearbyDevice(key, displayName)),
            (_, existing) => existing).Device;

    /// <summary>
    /// Tries to get the native handle previously registered under <paramref name="key"/>.
    /// </summary>
    public bool TryGetHandle(string key, [NotNullWhen(true)] out THandle? handle)
    {
        if (_peers.TryGetValue(key, out var entry))
        {
            handle = entry.Handle;
            return true;
        }

        handle = null;
        return false;
    }

    /// <summary>
    /// Tries to get the <see cref="NearbyDevice"/> previously registered under <paramref name="key"/>.
    /// </summary>
    public bool TryGetDevice(string key, [NotNullWhen(true)] out NearbyDevice? device)
    {
        if (_peers.TryGetValue(key, out var entry))
        {
            device = entry.Device;
            return true;
        }

        device = null;
        return false;
    }

    /// <summary>
    /// Removes the peer registered under <paramref name="key"/>, returning its
    /// <see cref="NearbyDevice"/> projection, or <see langword="null"/> if none was registered.
    /// </summary>
    public NearbyDevice? Remove(string key)
        => _peers.TryRemove(key, out var entry)
            ? entry.Device
            : null;

    /// <summary>
    /// Removes all tracked peers.
    /// </summary>
    public void Clear()
        => _peers.Clear();
}
