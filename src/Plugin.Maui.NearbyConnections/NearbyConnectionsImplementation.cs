using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;
using AdvertisingOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Implementation of the <see cref="INearbyConnections"/> interface.
/// </summary>
public partial class NearbyConnectionsImplementation : INearbyConnections//, IDisposable
{
    readonly IAdvertiserFactory _advertiserFactory;
    readonly IDiscovererFactory _discovererFactory;

    IAdvertiser? _advertiser;
    IDiscoverer? _discoverer;

    /// <inheritdoc/>
    public IAdvertiser Advertiser => _advertiser ?? _advertiserFactory.CreateAdvertiser();

    /// <inheritdoc/>
    public IDiscoverer Discoverer => _discoverer ?? _discovererFactory.CreateDiscoverer();

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsImplementation"/> class.
    /// </summary>
    /// <param name="advertiserFactory">
    /// The factory to create <see cref="IAdvertiser"/> instances.
    /// </param>
    /// <param name="discovererFactory">
    /// The factory to create <see cref="IDiscoverer"/> instances.
    /// </param>
    public NearbyConnectionsImplementation(
        IAdvertiserFactory advertiserFactory,
        IDiscovererFactory discovererFactory)
    {
        ArgumentNullException.ThrowIfNull(advertiserFactory);
        ArgumentNullException.ThrowIfNull(discovererFactory);

        _advertiserFactory = advertiserFactory;
        _discovererFactory = discovererFactory;
    }

    /// <inheritdoc/>
    public async Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await StopAdvertisingAsync(cancellationToken);

        _advertiser = _advertiserFactory.CreateAdvertiser();

        await _advertiser.StartAdvertisingAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (_advertiser?.IsAdvertising == true)
        {
            await _advertiser.StopAdvertisingAsync(cancellationToken);
            _advertiser.Dispose();
            _advertiser = null;
        }
    }

    /// <inheritdoc/>
    public async Task StartDiscoveryAsync(DiscoveringOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await StopDiscoveryAsync(cancellationToken);

        _discoverer = _discovererFactory.CreateDiscoverer();

        await _discoverer.StartDiscoveringAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        if (_discoverer?.IsDiscovering ?? false)
        {
            await _discoverer.StopDiscoveringAsync(cancellationToken);
            _discoverer.Dispose();
            _discoverer = null;
        }
    }
}