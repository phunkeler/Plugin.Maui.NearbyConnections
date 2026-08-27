namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyOptionsValidator
{
    static partial void PlatformValidate(NearbyOptions options, List<string> failures)
        => ServiceIdRules.Validate(
            options.ServiceId,
            options.ServiceId == ServiceIdRules.Unset
                ? ServiceIdRules.Suggest(AppInfo.Name)
                : null,
            failures);
}
