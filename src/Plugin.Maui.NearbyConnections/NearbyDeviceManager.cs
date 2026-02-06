namespace Plugin.Maui.NearbyConnections;

/// <inheritdoc />
sealed class NearbyDeviceManager : INearbyDeviceManager
{
    readonly TimeProvider _timeProvider;
    readonly NearbyConnectionsEvents _events;

    readonly ConcurrentDictionary<string, NearbyDevice> _devices = new();

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
        var device = _devices.GetOrAdd(id, _ => new NearbyDevice(id, displayName));
        SetState(id, NearbyDeviceState.Discovered);
        device.LastSeenAt = _timeProvider.GetUtcNow();
        return device;
    }

    public NearbyDevice GetOrAddDevice(string id, string? displayName, NearbyDeviceState initialState)
        => _devices.GetOrAdd(id, _ => new NearbyDevice(id, displayName) { State = initialState, LastSeenAt = _timeProvider.GetUtcNow() });

    public NearbyDevice? DeviceLost(string id)
        => _devices.TryRemove(id, out var device) ? device : null;

    public NearbyDevice? SetState(string id, NearbyDeviceState state)
    {
        if (!_devices.TryGetValue(id, out var device))
        {
            return null;
        }

        var previousState = device.State;
        device.State = state;

        if (previousState != state)
        {
            _events.OnDeviceStateChanged(device, previousState, _timeProvider.GetUtcNow());
        }

        return device;
    }

    public NearbyDevice? DeviceDisconnected(string id)
        => _devices.TryRemove(id, out var device) ? device : null;

    public void Clear()
        => _devices.Clear();
}
