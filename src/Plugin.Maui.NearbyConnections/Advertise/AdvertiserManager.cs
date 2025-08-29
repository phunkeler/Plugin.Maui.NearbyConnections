namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising operations for nearby peer-to-peer connections.
/// </summary>
public interface IAdvertiserManager
{
    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StartAdvertisingAsync(AdvertiseOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising for this session.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages advertising operations for nearby peer-to-peer connections.
/// </summary>
public class AdvertiserManager : IAdvertiserManager
{
    readonly IAdvertiserFactory _advertiserFactory;

    IAdvertiser? _advertiser;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvertiserManager"/> class.
    /// </summary>
    /// <param name="advertiserFactory"></param>
    public AdvertiserManager(IAdvertiserFactory advertiserFactory)
    {
        ArgumentNullException.ThrowIfNull(advertiserFactory);

        _advertiserFactory = advertiserFactory;
    }

    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAdvertisingAsync(AdvertiseOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await StopAdvertisingAsync(cancellationToken);

        _advertiser = _advertiserFactory.CreateAdvertiser();

        await _advertiser.StartAdvertisingAsync(options, cancellationToken);
    }

    /// <summary>
    /// Stops advertising for this session.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (_advertiser?.IsAdvertising == true)
        {
            await _advertiser.StopAdvertisingAsync(cancellationToken);
            _advertiser.Dispose();
            _advertiser = null;
        }
    }
}