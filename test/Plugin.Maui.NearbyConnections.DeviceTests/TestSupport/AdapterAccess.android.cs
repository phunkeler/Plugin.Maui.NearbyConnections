namespace Plugin.Maui.NearbyConnections.DeviceTests;

/// <summary>
/// Reaches the platform adapter behind a bridge, typed — the device tests drive the adapter's
/// SDK-typed callback entry points directly.
/// </summary>
static class AdapterAccess
{
    public static AndroidAdapter Android(this PlatformBridge bridge) => (AndroidAdapter)bridge.Adapter;
}
