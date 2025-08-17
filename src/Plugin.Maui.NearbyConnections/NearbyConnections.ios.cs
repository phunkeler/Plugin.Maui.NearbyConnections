using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Implementation of the INearbyConnections interface for iOS.
/// </summary>
public partial class NearbyConnectionsImplementation : INearbyConnections, IDisposable
{
    IAdvertiser? _advertiser;
    IDiscoverer? _discoverer;

    /// <inheritdoc/>
    public async Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_advertiser?.IsAdvertising ?? false)
        {
            await _advertiser.StopAdvertisingAsync(cancellationToken);
            _advertiser.Dispose();
            _advertiser = null;
        }

        _advertiser = _advertiserFactory.CreateAdvertiser();

        await _advertiser.StartAdvertisingAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (_advertiser?.IsAdvertising ?? false)
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

        if (_discoverer?.IsDiscovering ?? false)
        {
            await _discoverer.StopDiscoveringAsync(cancellationToken);
            _discoverer.Dispose();
            _discoverer = null;
        }

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

    /// <summary>
    /// Disposes of resources used by the implementation.
    /// </summary>
    public void Dispose()
    {
        _advertiser?.Dispose();
        _discoverer?.Dispose();

        _advertiser = null;
        _discoverer = null;

        GC.SuppressFinalize(this);
    }
}