namespace Plugin.Maui.NearbyConnections;

/// <inheritdoc />
internal sealed class NearbyDeviceManager : INearbyDeviceManager
{
    readonly ConcurrentDictionary<string, NearbyDevice> _devices = new();

    /// <inheritdoc />
    public IReadOnlyList<NearbyDevice> Devices
        => _devices.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public NearbyDevice DeviceFound(string id, string? displayName)
    {
        var device = _devices.GetOrAdd(id, _ => new NearbyDevice(id, displayName));
        device.State = NearbyDeviceState.Discovered;
        return device;
    }

    /// <inheritdoc />
    public NearbyDevice? DeviceLost(string id)
        => _devices.TryRemove(id, out var device) ? device : null;

    /// <inheritdoc />
    public NearbyDevice? SetState(string id, NearbyDeviceState state)
    {
        if (!_devices.TryGetValue(id, out var device))
        {
            return null;
        }

        device.State = state;
        return device;
    }

    /// <inheritdoc />
    public NearbyDevice? DeviceDisconnected(string id)
        => _devices.TryRemove(id, out var device) ? device : null;

    /// <inheritdoc />
    public void Clear()
        => _devices.Clear();
}
