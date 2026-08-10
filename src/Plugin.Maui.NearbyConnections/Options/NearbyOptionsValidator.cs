using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyOptionsValidator : IValidateOptions<NearbyOptions>
{
    // PlatformValidate is implemented on iOS only (NearbyOptionsValidator.ios.cs). On the other
    // targets it is an unimplemented partial and compiles away entirely, which is why no empty
    // per-platform body is needed — and why Sonar sees the failure check below as unconditional
    // there.
    [SuppressMessage("SonarAnalyzer.CSharp", "S2583:Conditions should not unconditionally evaluate to true or to false",
        Justification = "PlatformValidate is implemented on iOS, where it adds failures at runtime.")]
    public ValidateOptionsResult Validate(string? name, NearbyOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ServiceId))
        {
            failures.Add("ServiceId must not be null or empty.");
        }

        PlatformValidate(options, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    static partial void PlatformValidate(NearbyOptions options, List<string> failures);
}