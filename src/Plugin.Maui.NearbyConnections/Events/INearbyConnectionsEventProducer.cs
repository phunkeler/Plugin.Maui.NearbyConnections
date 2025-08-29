using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// A producer of <see cref="INearbyConnectionsEvent"/> instances.
/// </summary>
public interface INearbyConnectionsEventProducer : IAsyncDisposable
{
    /// <summary>
    /// Gets the channel reader to consume nearby connections events.
    /// </summary>
    ChannelReader<INearbyConnectionsEvent> Events { get; }

    /// <summary>
    /// Publishes a new event to the channel.
    /// </summary>
    Task PublishAsync(INearbyConnectionsEvent nearbyEvent, CancellationToken cancellationToken = default);
}