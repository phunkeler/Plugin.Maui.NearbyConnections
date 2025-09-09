using System.ComponentModel.DataAnnotations;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Advertise;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Options to configure the behavior of the Nearby Connections plugin.
/// </summary>
public class NearbyConnectionsOptions
{
    /// <summary>
    /// The configuration section name for these options.
    /// </summary>
    public const string SectionName = "NearbyConnections";

    /// <summary>
    /// Options for the advertiser (if used).
    /// </summary>
    public AdvertiseOptions AdvertiserOptions { get; init; } = new();

    /// <summary>
    /// Settings that control discovery behavior (if used).
    /// </summary>
    /// <remarks>
    /// This can differ from <see cref="AdvertiseOptions.ServiceName" /> in more complex use cases.
    /// </remarks>
    public DiscoverOptions DiscovererOptions { get; init; } = new();

    /// <summary>
    /// Configuration for the event publishing system.
    /// </summary>
    public EventPublisherOptions EventPublisher { get; init; } = new();

    /// <summary>
    /// When a nearby device requests a connection, automatically accept it.
    /// </summary>
    public bool AutoAcceptConnections { get; init; } = true;

    /// <summary>
    /// The default timeout for discovery operations.
    /// </summary>
    [Range(1, 300)]
    public int DiscoveryTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// The default timeout for advertising operations.
    /// </summary>
    [Range(1, 300)]
    public int AdvertisingTimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Configuration options for the event publishing system.
/// </summary>
public class EventPublisherOptions
{
    /// <summary>
    /// Enable correlation tracking for events.
    /// </summary>
    public bool EnableCorrelation { get; init; } = true;

    /// <summary>
    /// Enable deduplication of events within a time window.
    /// </summary>
    public bool EnableDeduplication { get; init; } = true;

    /// <summary>
    /// The time window for event deduplication in milliseconds.
    /// </summary>
    [Range(100, 10000)]
    public int DeduplicationWindowMs { get; init; } = 500;

    /// <summary>
    /// Maximum number of events to buffer in the pipeline.
    /// </summary>
    [Range(10, 10000)]
    public int MaxBufferSize { get; init; } = 1000;
}

sealed internal class NearbyConnectionsId
{
    private string _id = Guid.NewGuid().ToString();
    public string Id
    {
        get => _id;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Id cannot be null or whitespace.", nameof(value));
            _id = value;
        }
    }
}