namespace Plugin.Maui.NearbyConnections;

sealed partial class PeerRegistry
{
    readonly ConcurrentDictionary<string, NearbyDevice> _peers = new(StringComparer.Ordinal);

    public NearbyDevice Record(string key, string? displayName)
        => _peers.GetOrAdd(key, static (k, name) => new NearbyDevice(k, name), displayName);

    public bool TryGetDevice(string key, [NotNullWhen(true)] out NearbyDevice? device)
        => _peers.TryGetValue(key, out device);

    public NearbyDevice? Remove(string key)
    {
        PlatformRemove(key);

        return _peers.TryRemove(key, out var device)
            ? device
            : null;
    }

    public void Clear()
    {
        PlatformClear();
        _peers.Clear();
    }

    partial void PlatformRemove(string key);

    partial void PlatformClear();
}