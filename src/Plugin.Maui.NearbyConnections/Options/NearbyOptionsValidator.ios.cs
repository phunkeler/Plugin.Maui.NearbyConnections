namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyOptionsValidator
{
    static partial void PlatformValidate(NearbyOptions options, List<string> failures)
        => ServiceIdRules.Validate(
            options.ServiceId,
            // AppInfo.Name reads the app bundle, so it is derived only on the path that quotes it.
            options.ServiceId == ServiceIdRules.Unset ? ServiceIdRules.Suggest(AppInfo.Name) : null,
            failures);
}
