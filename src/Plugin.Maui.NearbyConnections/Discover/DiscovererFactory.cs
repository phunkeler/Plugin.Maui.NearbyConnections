using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Factory interface for creating <see cref="IDiscoverer"/> instances.
/// </summary>
public interface IDiscovererFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="IDiscoverer"/>.
    /// </summary>
    /// <returns>
    /// A new instance of <see cref="IDiscoverer"/>.
    /// </returns>
    IDiscoverer CreateDiscoverer();
}

/// <summary>
/// Factory implementation for creating <see cref="IDiscoverer"/> instances.
/// </summary>
public class DiscovererFactory : IDiscovererFactory
{
    readonly INearbyConnectionsEventProducer _eventProducer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscovererFactory"/> class.
    /// </summary>
    public DiscovererFactory(INearbyConnectionsEventProducer eventProducer)
    {
        ArgumentNullException.ThrowIfNull(eventProducer);

        _eventProducer = eventProducer;
    }

    /// <inheritdoc/>
    public IDiscoverer CreateDiscoverer() => new Discoverer(_eventProducer);
}