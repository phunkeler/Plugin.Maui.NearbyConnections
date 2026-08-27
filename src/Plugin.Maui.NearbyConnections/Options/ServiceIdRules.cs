using System.Text.RegularExpressions;

namespace Plugin.Maui.NearbyConnections;

static partial class ServiceIdRules
{
    internal const string Unset = "_UNSET";

    const string Reference =
        "See https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiser " +
        "and RFC 6335 section 5.1 (Service Name Syntax).";

    internal static void Validate(
        string serviceId,
        string? suggestion,
        List<string> failures)
    {
        if (serviceId == Unset)
        {
            var setTo = suggestion is null
                ? "Set it to a short protocol name such as 'abc-txtchat'."
                : $"Set it to a short protocol name, for example: options.ServiceId = \"{suggestion}\";";

            failures.Add(
                "ServiceId has not been set. On iOS it is passed directly as " +
                "MCNearbyServiceAdvertiser/MCNearbyServiceBrowser's serviceType, which has no " +
                "meaningful default. " + setTo + " " +
                "Note this is NOT a Bonjour '_name._tcp' service type; that longer form belongs " +
                "only in the app's Info.plist NSBonjourServices entries. " + Reference);
            return;
        }

        if (string.IsNullOrEmpty(serviceId))
        {
            return;
        }

        if (serviceId.Length > 15)
        {
            failures.Add(
                $"{nameof(NearbyOptions.ServiceId)} '{serviceId}' is {serviceId.Length} characters long. On iOS it must be " +
                $"1-15 characters. {Reference}");
        }

        if (!serviceId.All(IsAllowedCharacter))
        {
            var offending = new string([.. serviceId.Where(c => !IsAllowedCharacter(c)).Distinct()]);

            failures.Add(
                $"{nameof(NearbyOptions.ServiceId)} '{serviceId}' contains characters that iOS rejects: '{offending}'. " +
                $"Only ASCII lowercase letters, digits, and hyphens are permitted — uppercase " +
                $"letters, underscores, dots, and spaces are all invalid. {Reference}");
        }

        if (!serviceId.Any(char.IsAsciiLetterLower))
        {
            failures.Add(
                $"{nameof(NearbyOptions.ServiceId)} '{serviceId}' contains no ASCII letter. On iOS it must contain at " +
                $"least one. {Reference}");
        }

        if (serviceId.StartsWith('-') || serviceId.EndsWith('-'))
        {
            failures.Add(
                $"{nameof(NearbyOptions.ServiceId)} '{serviceId}' begins or ends with a hyphen, which iOS rejects. {Reference}");
        }

        if (serviceId.Contains("--", StringComparison.Ordinal))
        {
            failures.Add(
                $"{nameof(NearbyOptions.ServiceId)} '{serviceId}' contains adjacent hyphens, which iOS rejects. {Reference}");
        }
    }

    internal static string? Suggest(string? applicationName)
    {
        var candidate = Unsupported().Replace(applicationName?.ToLowerInvariant() ?? "", "-").Trim('-');

        if (candidate.Length > 15)
        {
            candidate = candidate[..15].TrimEnd('-');
        }

        return candidate.Any(char.IsAsciiLetterLower) ? candidate : null;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Unsupported();

    static bool IsAllowedCharacter(char c)
        => char.IsAsciiLetterLower(c)
            || char.IsAsciiDigit(c)
            || c == '-';
}
