using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <inheritdoc/>
public partial class Advertiser : IAdvertiser
{
    readonly INearbyConnectionsEventProducer _eventProducer;

    bool _isAdvertising;

    /// <inheritdoc />
    public event EventHandler<AdvertisingStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public bool IsAdvertising
    {
        get => _isAdvertising;
        private set
        {
            if (_isAdvertising != value)
            {
                _isAdvertising = value;
                StateChanged?.Invoke(this, new AdvertisingStateChangedEventArgs
                {
                    IsAdvertising = value
                });
            }
        }
    }

    /// <summary>
    /// The event producer for nearby connections events.
    /// </summary>
    /// <param name="eventProducer"></param>
    public Advertiser(INearbyConnectionsEventProducer eventProducer)
    {
        ArgumentNullException.ThrowIfNull(eventProducer);

        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsAdvertising)
        {
            await PlatformStartAdvertising(options, cancellationToken);
            IsAdvertising = true;
        }
    }

    /// <inheritdoc />
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (IsAdvertising)
        {
            await PlatformStopAdvertising(cancellationToken);
            IsAdvertising = false;
        }
    }
}