namespace Plugin.Maui.NearbyConnections;

interface INearbyDeviceManager
{
    void Clear();
    IReadOnlyList<NearbyDevice> Devices { get; }
    NearbyDevice RecordDeviceFound(string id, string? displayName);
    NearbyDevice? RemoveDevice(string id);
    bool TryGetDevice(string id, [NotNullWhen(true)] out NearbyDevice? device);
}

sealed class NearbyDeviceManager : INearbyDeviceManager
{
    readonly ConcurrentDictionary<string, NearbyDevice> _devices = [];

    public IReadOnlyList<NearbyDevice> Devices
        => _devices.Values.ToList().AsReadOnly();

    public NearbyDeviceManager() { }

    public NearbyDevice RecordDeviceFound(string id, string? displayName)
        => _devices.AddOrUpdate(id, _ => new NearbyDevice(id, displayName), (_, existing) => existing);

    public NearbyDevice? RemoveDevice(string id)
        => _devices.TryRemove(id, out var device)
            ? device
            : null;

    public bool TryGetDevice(string id, [NotNullWhen(true)] out NearbyDevice? device)
        => _devices.TryGetValue(id, out device);

    public void Clear()
        => _devices.Clear();
}
