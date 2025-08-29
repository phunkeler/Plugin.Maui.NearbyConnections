using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <inheritdoc/>
public partial class Advertiser : IAdvertiser
{
    readonly INearbyConnectionsEventProducer _eventProducer;

    /// <inheritdoc />
    public bool IsAdvertising { get; private set; }

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
    public async Task StartAdvertisingAsync(AdvertiseOptions options, CancellationToken cancellationToken = default)
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