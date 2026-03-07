namespace Plugin.Maui.NearbyConnections;

sealed class NearbyDeviceManager : INearbyDeviceManager
{
    readonly TimeProvider _timeProvider;
    readonly NearbyConnectionsEvents _events;

    readonly ConcurrentDictionary<string, NearbyDevice> _devices = [];

    public IReadOnlyList<NearbyDevice> Devices
        => _devices.Values.ToList().AsReadOnly();

    public NearbyDeviceManager(TimeProvider timeProvider, NearbyConnectionsEvents events)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(events);

        _timeProvider = timeProvider;
        _events = events;
    }

    public NearbyDevice DeviceFound(string id, string? displayName)
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

        var previousState = device.State;
        device.State = state;

        if (state == NearbyDeviceState.Discovered)
        {
            device.LastSeen = _timeProvider.GetUtcNow();
        }

        // Confirm the device is still tracked before firing the event.
        // A concurrent RemoveDevice between TryGetValue and here would otherwise
        // fire DeviceStateChanged for a device that has already been removed.
        if (previousState != state && _devices.ContainsKey(id))
        {
            _events.OnDeviceStateChanged(device, previousState, _timeProvider.GetUtcNow());
        }

        return device;
    }

    public bool TryGetDevice(string id, [NotNullWhen(true)] out NearbyDevice? device)
        => _devices.TryGetValue(id, out device);

    public void Clear()
        => _devices.Clear();
}
