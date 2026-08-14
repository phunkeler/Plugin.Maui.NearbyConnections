using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyOptionsValidator
{
    public static void Validate(NearbyOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ServiceId))
        {
            failures.Add("ServiceId must not be null or empty.");
        }

        PlatformValidate(options, failures);

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(string.Empty, typeof(NearbyOptions), failures);
        }
    }

    static partial void PlatformValidate(NearbyOptions options, List<string> failures);
}