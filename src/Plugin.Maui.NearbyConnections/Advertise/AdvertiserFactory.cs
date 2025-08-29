using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Factory interface for creating <see cref="IAdvertiser"/> instances.
/// </summary>
public interface IAdvertiserFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="IAdvertiser"/>.
    /// </summary>
    /// <returns>
    /// A new instance of <see cref="IAdvertiser"/>.
    /// </returns>
    IAdvertiser CreateAdvertiser();
}

/// <summary>
/// Factory implementation for creating <see cref="IAdvertiser"/> instances.
/// </summary>
public class AdvertiserFactory : IAdvertiserFactory
{
    readonly Func<INearbyConnectionsEventProducer> _eventProducerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvertiserFactory"/> class.
    /// </summary>
    public AdvertiserFactory(Func<INearbyConnectionsEventProducer> eventProducerFactory)
    {
        ArgumentNullException.ThrowIfNull(eventProducerFactory);

        _eventProducerFactory = eventProducerFactory;
    }

    public AdvertiserFactory(NearbyConnectionsEventProducer nearbyConnectionsEventProducer)
    {
    }

    /// <inheritdoc/>
    public IAdvertiser CreateAdvertiser() => new Advertiser(_eventProducerFactory());
}