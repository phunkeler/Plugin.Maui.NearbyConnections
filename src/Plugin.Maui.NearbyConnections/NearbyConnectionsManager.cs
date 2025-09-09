using Microsoft.Extensions.Options;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Default implementation of <see cref="INearbyConnectionsManager"/>.
/// </summary>
internal sealed class NearbyConnectionsManager : INearbyConnectionsManager
{
    readonly IOptions<NearbyConnectionsOptions> _options;
    readonly IAdvertiserFactory _advertiserFactory;
    readonly IDiscovererFactory _discovererFactory;
    readonly INearbyConnectionsEventPublisher _eventPublisher;

    public NearbyConnectionsManager(
        IOptions<NearbyConnectionsOptions> options,
        IAdvertiserFactory advertiserFactory,
        IDiscovererFactory discovererFactory,
        INearbyConnectionsEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(advertiserFactory);
        ArgumentNullException.ThrowIfNull(discovererFactory);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _options = options;
        _advertiserFactory = advertiserFactory;
        _discovererFactory = discovererFactory;
        _eventPublisher = eventPublisher;

    }

    public Task<INearbyConnectionsSession> CreateSessionAsync(NearbyConnectionsSessionOptions? options = default)
    {
        // Use the configured options if none provided
        var sessionOptions = options ?? new NearbyConnectionsSessionOptions();

        // If no custom options provided, use the configured defaults
        if (options == null)
        {
            sessionOptions.AdvertiseOptions.ServiceName = _options.Value.AdvertiserOptions.ServiceName;
            sessionOptions.DiscoverOptions.ServiceName = _options.Value.DiscovererOptions.ServiceName;
        }

        var session = new NearbyConnectionsSessionImplementation(
            sessionOptions,
            _eventPublisher,
            _advertiserFactory,
            _discovererFactory);

        return Task.FromResult<INearbyConnectionsSession>(session);
    }

    public void Dispose()
    {
        _eventPublisher?.Dispose();
    }
}

/// <summary>
/// Internal implementation of <see cref="INearbyConnectionsSession"/>.
/// </summary>
internal sealed class NearbyConnectionsSessionImplementation : INearbyConnectionsSession
{
    readonly NearbyConnectionsSessionOptions _options;
    readonly INearbyConnectionsEventPublisher _eventPublisher;
    readonly IAdvertiserFactory _advertiserFactory;
    readonly IDiscovererFactory _discovererFactory;

    IAdvertiser? _advertiser;
    IDiscoverer? _discoverer;

    public NearbyConnectionsSessionImplementation(
        NearbyConnectionsSessionOptions options,
        INearbyConnectionsEventPublisher eventPublisher,
        IAdvertiserFactory advertiserFactory,
        IDiscovererFactory discovererFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _advertiserFactory = advertiserFactory ?? throw new ArgumentNullException(nameof(advertiserFactory));
        _discovererFactory = discovererFactory ?? throw new ArgumentNullException(nameof(discovererFactory));
    }

    public IObservable<INearbyConnectionsEvent> Events => _eventPublisher.Events;

    public Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = null)
    {
        var options = advertiseOptions ?? _options.AdvertiseOptions;
        _advertiser ??= _advertiserFactory.CreateAdvertiser();
        return _advertiser.StartAdvertisingAsync(options);
    }

    public void StopAdvertising()
    {
        _advertiser?.StopAdvertising();
    }

    public Task StartDiscoveryAsync(DiscoverOptions? discoverOptions = null)
    {
        var options = discoverOptions ?? _options.DiscoverOptions;
        _discoverer ??= _discovererFactory.CreateDiscoverer();
        return _discoverer.StartDiscoveringAsync(options);
    }

    public void StopDiscovery()
    {
        _discoverer?.StopDiscovering();
    }

    public void Dispose()
    {
        _advertiser?.Dispose();
        _discoverer?.Dispose();
    }
}