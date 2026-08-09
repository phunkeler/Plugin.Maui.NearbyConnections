using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyOptionsValidator : IValidateOptions<NearbyOptions>
{
    [SuppressMessage("SonarAnalyzer.CSharp", "S2583:Conditions should not unconditionally evaluate to true or to false",
        Justification = "PlatformValidate is a partial method; platform-specific implementations may add failures at runtime.")]
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