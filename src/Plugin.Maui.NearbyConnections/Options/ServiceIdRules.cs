namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The rules Apple applies to a Multipeer Connectivity <c>serviceType</c>, expressed as pure string
/// validation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is a crash guard.</strong> An invalid <c>serviceType</c> makes
/// <c>MCNearbyServiceAdvertiser</c>'s native initializer raise an <c>NSInvalidArgumentException</c>,
/// which crosses the native/managed boundary as a fatal native crash — <em>not</em> a catchable
/// .NET exception. A consumer cannot defend against it with <c>try</c>/<c>catch</c>, so the only
/// effective defence is to reject the value before it ever reaches the platform.
/// </para>
/// <para>
/// Deliberately free of any iOS type reference, and compiled on every target framework, so the
/// rules can be unit tested on the plain <c>net10.0</c> target. Placing them in
/// <c>NearbyConnectionsOptionsValidator.ios.cs</c> would have shipped the guard untested, because
/// the unit test project targets <c>net10.0</c> and never compiles the iOS partial.
/// </para>
/// <para>
/// <strong>Why this is enforced at run time rather than at build time.</strong> A build-time check
/// shipped as a <c>build/*.targets</c> hook in the NuGet package was considered and rejected:
/// <c>ServiceId</c> is a C# expression inside the <c>AddNearbyConnections</c> configure lambda, not
/// an MSBuild property, so MSBuild cannot evaluate it. Such a check would have to parse source and
/// would silently pass any non-literal value — a configuration binding, a <c>const</c> from another
/// assembly, anything computed. A guard that catches only the literal case while appearing
/// authoritative is worse than none. This validator sees the resolved value and is therefore the
/// only place the rule can actually be enforced.
/// </para>
/// <para>
/// Rules quoted from Apple's <c>serviceType</c> parameter documentation, which defers to RFC 6335
/// section 5.1 (Service Name Syntax). The string: must be 1–15 characters long; can contain only
/// ASCII lowercase letters, numbers, and hyphens; must contain at least one ASCII letter; must not
/// begin or end with a hyphen; and must not contain hyphens adjacent to other hyphens.
/// </para>
/// </remarks>
static class ServiceIdRules
{
    /// <summary>The sentinel assigned on iOS, where there is no meaningful default.</summary>
    internal const string Unset = "_UNSET";

    const string Reference =
        "See https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiser " +
        "and RFC 6335 section 5.1 (Service Name Syntax).";

    /// <summary>
    /// Adds a failure message for every rule <paramref name="serviceId"/> violates.
    /// </summary>
    /// <remarks>
    /// Every rule is evaluated rather than returning at the first failure, so a developer fixing a
    /// value learns about all of its problems at once instead of one per rebuild.
    /// </remarks>
    internal static void Validate(string serviceId, List<string> failures)
    {
        if (serviceId == Unset)
        {
            failures.Add(
                "ServiceId has not been set. On iOS it is passed directly as " +
                "MCNearbyServiceAdvertiser/MCNearbyServiceBrowser's serviceType, which has no " +
                "meaningful default. Set it to a short protocol name such as 'abc-txtchat'. " +
                "Note this is NOT a Bonjour '_name._tcp' service type; that longer form belongs " +
                "only in the app's Info.plist NSBonjourServices entries. " + Reference);
            return;
        }

        // A null/empty ServiceId is already reported by the shared validator. Re-reporting it here
        // as four more rule violations would bury the actual problem.
        if (string.IsNullOrEmpty(serviceId))
        {
            return;
        }

        if (serviceId.Length > 15)
        {
            failures.Add(
                $"ServiceId '{serviceId}' is {serviceId.Length} characters long. On iOS it must be " +
                $"1-15 characters. {Reference}");
        }

        if (!serviceId.All(IsAllowedCharacter))
        {
            var offending = new string([.. serviceId.Where(c => !IsAllowedCharacter(c)).Distinct()]);

            failures.Add(
                $"ServiceId '{serviceId}' contains characters that iOS rejects: '{offending}'. " +
                $"Only ASCII lowercase letters, digits, and hyphens are permitted — uppercase " +
                $"letters, underscores, dots, and spaces are all invalid. {Reference}");
        }

        if (!serviceId.Any(char.IsAsciiLetterLower))
        {
            failures.Add(
                $"ServiceId '{serviceId}' contains no ASCII letter. On iOS it must contain at " +
                $"least one. {Reference}");
        }

        if (serviceId.StartsWith('-') || serviceId.EndsWith('-'))
        {
            failures.Add(
                $"ServiceId '{serviceId}' begins or ends with a hyphen, which iOS rejects. {Reference}");
        }

        if (serviceId.Contains("--", StringComparison.Ordinal))
        {
            failures.Add(
                $"ServiceId '{serviceId}' contains adjacent hyphens, which iOS rejects. {Reference}");
        }
    }

    static bool IsAllowedCharacter(char c)
        => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-';
}
