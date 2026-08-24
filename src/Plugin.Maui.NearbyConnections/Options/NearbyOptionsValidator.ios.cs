namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyOptionsValidator
{
    static partial void PlatformValidate(NearbyOptions options, List<string> failures)
    {
        ServiceIdRules.Validate(
            options.ServiceId,
            // AppInfo.Name reads the app bundle, so it is derived only on the path that quotes it.
            options.ServiceId == ServiceIdRules.Unset ? ServiceIdRules.Suggest(AppInfo.Name) : null,
            failures);

        // Same class of guard as ServiceId: MCPeerID's initializer raises a native exception on an
        // empty or over-long name, which no consumer try/catch can intercept.
        DisplayNameRules.Validate(options.DisplayName, failures);
    }
}
