using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <inheritdoc/>
public partial class Advertiser : IAdvertiser
{
    readonly INearbyConnectionsEventProducer _eventProducer;

    /// <inheritdoc />
    public bool IsAdvertising { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="Advertiser"/> .
    /// </summary>
    /// <param name="eventProducer"></param>
    public Advertiser(INearbyConnectionsEventProducer eventProducer)
    {
        ArgumentNullException.ThrowIfNull(eventProducer);

        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task StartAdvertisingAsync(AdvertiseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsAdvertising)
        {
            await PlatformStartAdvertising(options);
            IsAdvertising = true;
        }
    }

    /// <inheritdoc />
    public void StopAdvertising()
    {
        if (IsAdvertising)
        {
            PlatformStopAdvertising();
            IsAdvertising = false;
        }
    }
}