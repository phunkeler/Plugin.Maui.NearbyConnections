using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using AdvertisingOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Implementation of the <see cref="INearbyConnections"/> interface.
/// </summary>
public class NearbyConnectionsImplementation : INearbyConnections, IDisposable
{
    readonly IAdvertiserFactory _advertiserFactory;
    readonly IDiscovererFactory _discovererFactory;

    IAdvertiser? _advertiser;
    IDiscoverer? _discoverer;

    /// <summary>
    /// Fired when the advertising state changes.
    /// This event remains active across advertiser dispose/recreate cycles.
    /// </summary>
    public event EventHandler<AdvertisingStateChangedEventArgs>? AdvertisingStateChanged;

    /// <summary>
    /// Fired when the discovering state changes.
    /// This event remains active across discoverer dispose/recreate cycles.
    /// </summary>
    public event EventHandler<DiscoveringStateChangedEventArgs>? DiscoveringStateChanged;

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
        _advertiser.AdvertisingStateChanged += OnAdvertiserStateChanged;

        await _advertiser.StartAdvertisingAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (_advertiser?.IsAdvertising == true)
        {
            await _advertiser.StopAdvertisingAsync(cancellationToken);
            _advertiser.AdvertisingStateChanged -= OnAdvertiserStateChanged;
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
        _discoverer.DiscoveringStateChanged += OnDiscovererStateChanged;

        await _discoverer.StartDiscoveringAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        if (_discoverer?.IsDiscovering ?? false)
        {
            await _discoverer.StopDiscoveringAsync(cancellationToken);
            _discoverer.DiscoveringStateChanged -= OnDiscovererStateChanged;
            _discoverer.Dispose();
            _discoverer = null;
        }
    }

    /// <summary>
    /// Disposes of resources used by the implementation.
    /// </summary>
    public void Dispose()
    {
        if (_advertiser != null)
        {
            _advertiser.AdvertisingStateChanged -= OnAdvertiserStateChanged;
            _advertiser.Dispose();
            _advertiser = null;
        }

        if (_discoverer != null)
        {
            _discoverer.DiscoveringStateChanged -= OnDiscovererStateChanged;
            _discoverer.Dispose();
            _discoverer = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Forwards advertiser state changes to consumers of the INearbyConnections interface.
    /// </summary>
    /// <param name="sender">The advertiser that fired the event</param>
    /// <param name="e">The event arguments</param>
    private void OnAdvertiserStateChanged(object? sender, AdvertisingStateChangedEventArgs e)
        => AdvertisingStateChanged?.Invoke(this, e);

    /// <summary>
    /// Forwards discoverer state changes to consumers of the INearbyConnections interface.
    /// </summary>
    /// <param name="sender">The discoverer that fired the event</param>
    /// <param name="e">The event arguments</param>
    private void OnDiscovererStateChanged(object? sender, DiscoveringStateChangedEventArgs e)
        => DiscoveringStateChanged?.Invoke(this, e);
}