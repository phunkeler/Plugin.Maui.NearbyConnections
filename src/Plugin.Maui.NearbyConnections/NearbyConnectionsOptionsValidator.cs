using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Validates <see cref="NearbyConnectionsOptions"/> configuration.
/// </summary>
internal sealed class NearbyConnectionsOptionsValidator : IValidateOptions<NearbyConnectionsOptions>
{
    public ValidateOptionsResult Validate(string? name, NearbyConnectionsOptions options)
    {
        var failures = new List<string>();

        // Validate discovery timeout
        if (options.DiscoveryTimeoutSeconds is < 1 or > 300)
        {
            failures.Add($"{nameof(options.DiscoveryTimeoutSeconds)} must be between 1 and 300 seconds.");
        }

        // Validate advertising timeout
        if (options.AdvertisingTimeoutSeconds is < 1 or > 300)
        {
            failures.Add($"{nameof(options.AdvertisingTimeoutSeconds)} must be between 1 and 300 seconds.");
        }

        // Validate event publisher options
        var eventPublisherResult = ValidateEventPublisherOptions(options.EventPublisher);
        if (eventPublisherResult.Failed)
        {
            failures.AddRange(eventPublisherResult.Failures);
        }

        // Validate service names are not empty
        if (string.IsNullOrWhiteSpace(options.AdvertiserOptions.ServiceName))
        {
            failures.Add($"{nameof(options.AdvertiserOptions.ServiceName)} cannot be null or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(options.DiscovererOptions.ServiceName))
        {
            failures.Add($"{nameof(options.DiscovererOptions.ServiceName)} cannot be null or whitespace.");
        }

        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult ValidateEventPublisherOptions(EventPublisherOptions options)
    {
        var failures = new List<string>();

        if (options.DeduplicationWindowMs is < 100 or > 10000)
        {
            failures.Add($"{nameof(options.DeduplicationWindowMs)} must be between 100 and 10000 milliseconds.");
        }

        if (options.MaxBufferSize is < 10 or > 10000)
        {
            failures.Add($"{nameof(options.MaxBufferSize)} must be between 10 and 10000.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}