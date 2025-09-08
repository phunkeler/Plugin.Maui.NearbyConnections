using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Manages discovering for nearby connections.
/// </summary>
public partial class Discoverer : IDiscoverer
{
    readonly INearbyConnectionsEventPublisher _eventPublisher;

    /// <inheritdoc />
    public bool IsDiscovering { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="Discoverer"/> .
    /// </summary>
    /// <param name="eventPublisher"></param>
    public Discoverer(INearbyConnectionsEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc />
    public async Task StartDiscoveringAsync(DiscoverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsDiscovering)
        {
            await PlatformStartDiscovering(options);
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
