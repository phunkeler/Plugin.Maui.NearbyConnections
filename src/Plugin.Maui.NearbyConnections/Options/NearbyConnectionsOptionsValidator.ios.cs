namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsOptionsValidator
{
    /// <summary>
    /// Rejects a <see cref="NearbyConnectionsOptions.ServiceId"/> that Multipeer Connectivity would
    /// refuse, at application startup, before it can reach the platform.
    /// </summary>
    /// <remarks>
    /// The rules live in <see cref="ServiceIdRules"/> so they compile on every target framework and
    /// can be unit tested; this partial only wires them into the options pipeline. See
    /// <see cref="ServiceIdRules"/> for why an invalid value must be caught here rather than
    /// handled by the consumer.
    /// </remarks>
    static partial void PlatformValidate(NearbyConnectionsOptions options, List<string> failures)
        => ServiceIdRules.Validate(options.ServiceId, failures);
}
