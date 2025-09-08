using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <inheritdoc/>
public partial class Advertiser : IAdvertiser
{
    readonly INearbyConnectionsEventPublisher _eventPublisher;

    /// <inheritdoc />
    public bool IsAdvertising { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="Advertiser"/> .
    /// </summary>
    /// <param name="eventPublisher"></param>
    public Advertiser(INearbyConnectionsEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _eventPublisher = eventPublisher;
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