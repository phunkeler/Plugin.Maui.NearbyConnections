using System.Threading.Channels;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Implementation of the <see cref="INearbyConnections"/> interface.
/// </summary>
public partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly INearbyConnectionsEventProducer _eventProducer;

    /// <inheritdoc/>
    public IAdvertiserManager Advertise { get; }

    /// <inheritdoc/>
    public IDiscovererManager Discover { get; }

    /// <inheritdoc/>
    public ChannelReader<INearbyConnectionsEvent> Events => _eventProducer.Events;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsImplementation"/> class.
    /// </summary>
    /// <param name="advertiserManager"></param>
    /// <param name="discovererManager"></param>
    /// <param name="eventProducer"></param>
    public NearbyConnectionsImplementation(
        IAdvertiserManager advertiserManager,
        IDiscovererManager discovererManager,
        INearbyConnectionsEventProducer eventProducer)
    {
        ArgumentNullException.ThrowIfNull(advertiserManager);
        ArgumentNullException.ThrowIfNull(discovererManager);
        ArgumentNullException.ThrowIfNull(eventProducer);

        Advertise = advertiserManager;
        Discover = discovererManager;
        _eventProducer = eventProducer;
    }
}