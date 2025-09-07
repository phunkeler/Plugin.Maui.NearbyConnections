using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Manages discovering for nearby connections.
/// </summary>
public partial class Discoverer : IDiscoverer
{
    readonly INearbyConnectionsEventProducer _eventProducer;

    /// <inheritdoc />
    public bool IsDiscovering { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="Discoverer"/> .
    /// </summary>
    /// <param name="eventProducer"></param>
    public Discoverer(INearbyConnectionsEventProducer eventProducer)
    {
        ArgumentNullException.ThrowIfNull(eventProducer);

        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task StartDiscoveringAsync(DiscoverOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsDiscovering)
        {
            await PlatformStartDiscovering(options, cancellationToken);
            IsDiscovering = true;
        }
    }

    /// <inheritdoc />
    public void StopDiscovering()
    {
        if (IsDiscovering)
        {
            PlatformStopDiscovering();
            IsDiscovering = false;
        }
    }
}
