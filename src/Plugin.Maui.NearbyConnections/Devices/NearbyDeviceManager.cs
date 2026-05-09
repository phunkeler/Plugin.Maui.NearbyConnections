namespace Plugin.Maui.NearbyConnections;

interface INearbyDeviceManager
{
    void Clear();
    IReadOnlyList<NearbyDevice> Devices { get; }
    NearbyDevice RecordDeviceFound(string id, string? displayName);
    NearbyDevice GetOrAddDevice(string id, string? displayName, NearbyDeviceState initialState);
    NearbyDevice? RemoveDevice(string id);
    NearbyDevice? SetState(string id, NearbyDeviceState state);
    bool TryGetDevice(string id, [NotNullWhen(true)] out NearbyDevice? device);
}

sealed class NearbyDeviceManager : INearbyDeviceManager
{
    readonly TimeProvider _timeProvider;

    readonly ConcurrentDictionary<string, NearbyDevice> _devices = [];

    public IReadOnlyList<NearbyDevice> Devices
        => _devices.Values.ToList().AsReadOnly();

    public NearbyDeviceManager(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    public NearbyDevice RecordDeviceFound(string id, string? displayName)
    {
        var now = _timeProvider.GetUtcNow();
        return _devices.AddOrUpdate(
            id,
            addValueFactory: x => new NearbyDevice(x, displayName) { State = NearbyDeviceState.Discovered, LastSeen = now },
            updateValueFactory: (_, existing) => { existing.LastSeen = now; return existing; });
    }

    public NearbyDevice GetOrAddDevice(string id, string? displayName, NearbyDeviceState initialState)
        => _devices.GetOrAdd(id, x => new NearbyDevice(x, displayName) { State = initialState, LastSeen = _timeProvider.GetUtcNow() });

    public NearbyDevice? RemoveDevice(string id)
        => _devices.TryRemove(id, out var device)
            ? device
            : null;

    public NearbyDevice? SetState(string id, NearbyDeviceState state)
    {
        if (!_devices.TryGetValue(id, out var device))
        {
            return null;
        }

        device.State = state;

        if (state == NearbyDeviceState.Discovered)
        {
            device.LastSeen = _timeProvider.GetUtcNow();
        }

        return device;
    }

    public bool TryGetDevice(string id, [NotNullWhen(true)] out NearbyDevice? device)
        => _devices.TryGetValue(id, out device);

    public void Clear()
        => _devices.Clear();
}
