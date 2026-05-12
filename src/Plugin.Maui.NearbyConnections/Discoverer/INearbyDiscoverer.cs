namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A Tier-2 discoverer service that manages the discovery lifecycle, nearby visible devices,
/// and active connections on the discoverer side.
/// </summary>
public interface INearbyDiscoverer
{
    /// <summary>
    /// Gets a value indicating whether the discover loop is currently running.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Starts the background discover loop. Returns <see cref="Task.CompletedTask"/> once the loop
    /// is launched (fire-and-forget internally).
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes once the loop has been started.</returns>
    Task StartAsync();

    /// <summary>
    /// Cancels the discover loop and returns immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes immediately after the loop is signalled to stop.</returns>
    Task StopAsync();

    /// <summary>
    /// Delegates to <see cref="INearbyConnections.ConnectAsync"/>, adds the returned connection to
    /// active connections, and starts monitoring and payload forwarding.
    /// </summary>
    /// <remarks>
    /// If the underlying <see cref="INearbyConnections.ConnectAsync"/> call throws, the device
    /// is not re-added to the visible set. The platform may re-fire a
    /// <c>FoundPeer</c> event that restores it — this is a known behavior.
    /// </remarks>
    /// <param name="device">The device to connect to.</param>
    /// <param name="cancellationToken">A token to cancel the connect operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to the established <see cref="NearbyConnection"/>.
    /// </returns>
    Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a unified stream of device discovery lifecycle and payload events.
    /// The first batch of items reflects current state (visible devices and active connections)
    /// as synthetic events, followed by live events as they occur.
    /// Completes when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">A token to stop enumerating the unified stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="DiscovererEvent"/> instances.
    /// </returns>
    IAsyncEnumerable<DiscovererEvent> EventsAsync(CancellationToken cancellationToken = default);
}
