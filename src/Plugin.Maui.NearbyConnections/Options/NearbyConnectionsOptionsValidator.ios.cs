using System.Text.RegularExpressions;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsOptionsValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9\-]+$")]
    private static partial Regex BonjourServiceTypeRegex();

    static partial void PlatformValidate(NearbyConnectionsOptions options, List<string> failures)
    {
        if (options.ServiceId.Length > 15)
            failures.Add(
                $"ServiceId '{options.ServiceId}' exceeds the 15-character Bonjour limit ({options.ServiceId.Length} chars).");

        if (!BonjourServiceTypeRegex().IsMatch(options.ServiceId))
            failures.Add(
                $"ServiceId '{options.ServiceId}' contains characters invalid for a Bonjour service type (alphanumeric and hyphens only).");
    }
}
