namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyOptions
{
    // The same semantic default the device platforms get from DeviceInfo.Name (the machine name),
    // truncated to the shared display-name cap so an unusual host name cannot fail validation.
    // DisplayName validation is shared across every target framework, so this target needs a valid
    // default too — a consumer's own unit test project calls AddNearby on net10.0.
    private static partial string GetDefaultDisplayName()
        => PeerLookup.Sanitize(Environment.MachineName, DisplayNameRules.MaxBytes) ?? "nearby-device";

    private static partial string GetDefaultServiceId() => string.Empty;
}
