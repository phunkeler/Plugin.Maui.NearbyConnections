namespace Plugin.Maui.NearbyConnections.Device;

internal sealed class NearbyDevice(
    string id,
    string displayName,
    NearbyDeviceStatus status) : INearbyDevice
{
    public string Id { get; } = id;

    public string DisplayName { get; } = displayName;

    public NearbyDeviceStatus Status { get; internal set; } = status;
}