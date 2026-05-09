namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsOptionsValidator : IValidateOptions<NearbyConnectionsOptions>
{
    [SuppressMessage("SonarAnalyzer.CSharp", "S2583:Conditions should not unconditionally evaluate to true or to false",
        Justification = "PlatformValidate is a partial method; platform-specific implementations may add failures at runtime.")]
    public ValidateOptionsResult Validate(string? name, NearbyConnectionsOptions options)
    {
        var failures = new List<string>();
        PlatformValidate(options, failures);
        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    static partial void PlatformValidate(NearbyConnectionsOptions options, List<string> failures);
}