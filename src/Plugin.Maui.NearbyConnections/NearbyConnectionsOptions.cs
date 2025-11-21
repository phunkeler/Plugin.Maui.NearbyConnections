using System.ComponentModel.DataAnnotations;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One-time app configuration for Nearby Connections.
/// Configure once at startup via <see cref="NearbyConnections.Configure"/>.
/// </summary>
public class NearbyConnectionsOptions
{
    /// <summary>
    /// The configuration section name for these options.
    /// </summary>
    public const string SectionName = "NearbyConnections";

    /// <summary>
    /// Service identifier for iOS Bonjour and Android service ID.
    /// Must match Info.plist NSBonjourServices configuration on iOS.
    /// </summary>
    public string ServiceName { get; set; } = AppInfo.Current.Name;

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

#if ANDROID

    /// <summary>
    /// The <see cref="Android.App.Activity"/> (required for Google Nearby Connections API).
    /// </summary>
    public Android.App.Activity? Activity { get; set; } = Platform.CurrentActivity;

    /// <summary>
    /// Gets or sets the advertising/discovery strategy.
    /// Must match between advertising and discovery sessions.
    /// Default is <see cref="Strategy.P2pCluster"/>.
    /// </summary>
    public Strategy Strategy { get; set; } = Strategy.P2pCluster;

    /// <summary>
    /// Gets or sets whether low power mode should be used.
    /// If <see langword="true" />, only low power mediums (like BLE) will be used for advertising and discovery.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool UseLowPower { get; set; }

    /// <summary>
    /// Gets or sets the Android connection type.
    /// Default is <see cref="Android.Gms.Nearby.Connection.ConnectionType.Balanced"/>.
    /// </summary>
    public int ConnectionType { get; set; } = Android.Gms.Nearby.Connection.ConnectionType.Balanced;
#endif
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

    /// <summary>
    /// Factory functions for creating event processors.
    /// These are invoked when creating a new NearbyConnectionsImplementation instance.
    /// </summary>
    /// <remarks>
    /// Use factory functions instead of instances to support DI resolution.
    /// Example: ProcessorFactories.Add(sp => sp.GetRequiredService&lt;MyProcessor&gt;())
    /// </remarks>
    public List<Func<IServiceProvider, IEventProcessor>> ProcessorFactories { get; init; } = new();

    /// <summary>
    /// Adds an event processor to the pipeline using a factory function.
    /// Follows Sentry's AddEventProcessor pattern.
    /// </summary>
    /// <param name="processorFactory">
    /// Factory function that creates the processor instance, with access to the service provider.
    /// </param>
    /// <returns>This options instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// options.EventPublisher.AddEventProcessor(sp => sp.GetRequiredService&lt;LoggingProcessor&gt;());
    /// options.EventPublisher.AddEventProcessor(_ => new FilterProcessor());
    /// </code>
    /// </example>
    public EventPublisherOptions AddEventProcessor(Func<IServiceProvider, IEventProcessor> processorFactory)
    {
        ArgumentNullException.ThrowIfNull(processorFactory);
        ProcessorFactories.Add(processorFactory);
        return this;
    }

    /// <summary>
    /// Adds an event processor to the pipeline by type.
    /// The processor will be resolved from the service provider.
    /// Follows Sentry's AddEventProcessor&lt;T&gt; pattern.
    /// </summary>
    /// <typeparam name="TProcessor">The event processor type to add.</typeparam>
    /// <returns>This options instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Register processor in DI first
    /// builder.Services.AddSingleton&lt;LoggingProcessor&gt;();
    ///
    /// // Then add to pipeline
    /// builder.Services.AddNearbyConnections(options =>
    /// {
    ///     options.EventPublisher.AddEventProcessor&lt;LoggingProcessor&gt;();
    /// });
    /// </code>
    /// </example>
    public EventPublisherOptions AddEventProcessor<TProcessor>()
        where TProcessor : IEventProcessor
    {
        ProcessorFactories.Add(sp =>
        {
            if (sp.GetService(typeof(TProcessor)) is not IEventProcessor processor)
            {
                throw new InvalidOperationException(
                    $"Event processor '{typeof(TProcessor).Name}' is not registered in the service collection. " +
                    $"Register it with services.AddSingleton<{typeof(TProcessor).Name}>() before adding to the pipeline.");
            }
            return processor;
        });
        return this;
    }

    /// <summary>
    /// Adds an event processor instance directly to the pipeline.
    /// Use this for processors that don't need dependency injection.
    /// </summary>
    /// <param name="processor">The processor instance to add.</param>
    /// <returns>This options instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// options.EventPublisher.AddEventProcessor(new SimpleFilterProcessor());
    /// </code>
    /// </example>
    public EventPublisherOptions AddEventProcessor(IEventProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ProcessorFactories.Add(_ => processor);
        return this;
    }
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