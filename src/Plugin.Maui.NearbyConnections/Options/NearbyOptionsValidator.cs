using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyOptionsValidator
{
    /// <summary>
    /// Validates <paramref name="options"/>, throwing immediately if it is unusable.
    /// </summary>
    /// <remarks>
    /// Called synchronously from <see cref="ServiceCollectionExtensions.AddNearby"/>, not through
    /// the <c>Microsoft.Extensions.Options</c> pipeline: MAUI apps never run
    /// <c>IHost.StartAsync</c>, so <c>IValidateOptions&lt;T&gt;</c> plus <c>ValidateOnStart()</c> —
    /// the ASP.NET Core convention for this — has no trigger and never runs. Calling this directly
    /// at registration time gets the same fail-fast behavior without depending on a pipeline that
    /// does not exist here.
    /// </remarks>
    // PlatformValidate is implemented on iOS only (NearbyOptionsValidator.ios.cs). On the other
    // targets it is an unimplemented partial and compiles away entirely, which is why no empty
    // per-platform body is needed — and why Sonar sees the failure check below as unconditional
    // there.
    [SuppressMessage("SonarAnalyzer.CSharp", "S2583:Conditions should not unconditionally evaluate to true or to false",
        Justification = "PlatformValidate is implemented on iOS, where it adds failures at runtime.")]
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
