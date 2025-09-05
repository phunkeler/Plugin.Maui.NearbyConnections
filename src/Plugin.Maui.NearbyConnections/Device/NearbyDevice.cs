namespace Plugin.Maui.NearbyConnections.Device;

public sealed partial class NearbyDevice : INearbyDevice
{
    public string Id { get; } = Guid.NewGuid().ToString();

    public string DisplayName { get; } = "Unknown";
}