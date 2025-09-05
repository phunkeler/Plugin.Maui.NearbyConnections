namespace Plugin.Maui.NearbyConnections.Device;

public sealed partial class NearbyDevice
{
    internal NearbyDevice(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    internal object GetPlatformHandle() => Id;
}