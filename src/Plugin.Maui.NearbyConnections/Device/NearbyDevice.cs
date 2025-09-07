namespace Plugin.Maui.NearbyConnections.Device;

public sealed partial class NearbyDevice(string id, string displayName) : INearbyDevice
{
    public string Id { get; } = id;

    public string DisplayName { get; } = displayName;
}