using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyDevices;

sealed partial class NearbyDevicesOptionsValidator : IValidateOptions<NearbyDevicesOptions>
{
    [SuppressMessage("SonarAnalyzer.CSharp", "S2583:Conditions should not unconditionally evaluate to true or to false",
        Justification = "PlatformValidate is a partial method; platform-specific implementations may add failures at runtime.")]
    public ValidateOptionsResult Validate(string? name, NearbyDevicesOptions options)
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

    static partial void PlatformValidate(NearbyDevicesOptions options, List<string> failures);
}