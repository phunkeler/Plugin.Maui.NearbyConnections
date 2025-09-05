using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The high-level session object.
/// </summary>
public interface INearbyConnectionsSession : IDisposable
{
    /// <summary>
    /// Start advertising this device.
    /// </summary>
    /// <returns></returns>
    Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = null);

    /// <summary>
    /// Stop advertising this device.
    /// </summary>
    /// <returns></returns>
    void StopAdvertising();

    /// <summary>
    /// Start discovering nearby devices.
    /// </summary>
    /// <returns></returns>
    Task StartDiscoveryAsync(DiscoverOptions? discoverOptions = null);

    /// <summary>
    /// Stop discovering nearby devices.
    /// </summary>
    /// <returns></returns>
    Task StopDiscovery();

    /// <summary>
    /// A provider for pushed-based notifications.
    /// </summary>
    IObservable<INearbyConnectionsEvent> Events { get; }
}

/// <summary>
/// The impl.
/// </summary>
public class NearbyConnectionsSession : INearbyConnectionsSession
{
    readonly NearbyConnectionsSessionOptions _options;
    readonly IAdvertiserFactory _advertiserFactory;
    readonly IDiscovererFactory _discovererFactory;

    IAdvertiser? _advertiser;
    IDiscoverer? _discoverer;

    /// <summary>
    /// Is this session currently advertising this device.
    /// </summary>
    public bool IsAdvertising => _advertiser?.IsAdvertising == true;

    /// <summary>
    /// Is is discovering?
    /// </summary>
    public bool IsDiscovering => _discoverer?.IsDiscovering == true;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="advertiserFactory"></param>
    /// <param name="discovererFactory"></param>
    public NearbyConnectionsSession(
        NearbyConnectionsSessionOptions options,
        IAdvertiserFactory advertiserFactory,
        IDiscovererFactory discovererFactory)
    {
        _options = options;
        _advertiserFactory = advertiserFactory;
        _discovererFactory = discovererFactory;
    }

    /// <inheritdoc/>
    public IObservable<INearbyConnectionsEvent> Events => throw new NotImplementedException();

    /// <inheritdoc/>
    public async Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = null)
    {
        advertiseOptions ??= _options.AdvertiseOptions;

        StopAdvertising();

        _advertiser = _advertiserFactory.CreateAdvertiser();

        await _advertiser.StartAdvertisingAsync(advertiseOptions);
    }

    /// <inheritdoc/>
    public async Task StartDiscoveryAsync(DiscoverOptions? discoverOptions = null)
    {
        discoverOptions ??= _options.DiscoverOptions;

        await StopDiscovery();

        _discoverer = _discovererFactory.CreateDiscoverer();

        await _discoverer.StartDiscoveringAsync(discoverOptions);
    }

    /// <inheritdoc/>
    public void StopAdvertising()
    {
        if (_advertiser?.IsAdvertising == true)
        {
            _advertiser.StopAdvertising();
            _advertiser.Dispose();
            _advertiser = null;
        }
    }

    /// <inheritdoc/>
    public async Task StopDiscovery()
    {
        if (_discoverer?.IsDiscovering ?? false)
        {
            await _discoverer.StopDiscoveringAsync();
            _discoverer.Dispose();
            _discoverer = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}