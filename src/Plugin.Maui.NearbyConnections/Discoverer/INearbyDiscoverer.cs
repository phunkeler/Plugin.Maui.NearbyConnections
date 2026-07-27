namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A Tier-2 discoverer service that manages the discovery lifecycle, nearby visible devices,
/// and active connections on the discoverer side.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Shutdown sequence.</strong> To fully stop discovering and release resources, call
/// <see cref="StopAsync"/> to cancel the platform operation and emit any cleanup events, then
/// dispose the instance. <see cref="StopAsync"/> does <strong>not</strong> complete subscriber
/// channels — only <see cref="IAsyncDisposable.DisposeAsync"/> (or <see cref="IDisposable.Dispose"/>)
/// completes subscriber channels, which causes any active <see langword="await foreach"/> loop
/// over <see cref="EventsAsync"/> to drain and exit naturally.
/// </para>
/// <para>
/// Exiting the <see langword="await foreach"/> loop — via <see langword="break"/>, or by
/// cancelling the <c>cancellationToken</c> passed to <see cref="EventsAsync"/> (which ends the
/// enumeration with an <see cref="OperationCanceledException"/>) — only detaches the current
/// observer; the background discovery loop and the underlying platform operation continue
/// running until <see cref="StopAsync"/> is called. Use the pattern below for clean teardown —
/// exit the loop, then stop, then dispose (disposing completes the channels of any remaining
/// subscribers):
/// </para>
/// <code>
/// await discoverer.StartAsync();
///
/// await foreach (var ev in discoverer.EventsAsync(cancellationToken))
/// {
///     // handle ev; break when done observing
///     if (done)
///     {
///         break;
///     }
/// }
///
/// await discoverer.StopAsync();    // stops the platform discovery operation, emits cleanup events
/// await discoverer.DisposeAsync(); // completes remaining subscriber channels
/// </code>
/// <para>
/// <strong>Thread safety.</strong>
/// All methods are safe to call from any thread. <see cref="ConnectAsync"/> is safe to call
/// concurrently for different <see cref="NearbyDevice"/> instances. <see cref="EventsAsync"/>
/// is safe to call concurrently — each caller receives its own independent event stream.
/// Platform callbacks (device found/lost, disconnections) are delivered on SDK-owned background
/// threads; do not assume any particular thread when handling <see cref="DiscovererEvent"/>
/// items yielded from <see cref="EventsAsync"/>.
/// </para>
/// </remarks>
public interface INearbyDiscoverer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the discover loop is currently running.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Starts the background discover loop. If a previous discover loop is still winding down
    /// (for example, from a prior <see cref="StartAsync"/> or <see cref="StopAsync"/> call), this
    /// method first awaits its complete teardown before starting the new one, so at most one
    /// discover loop is ever in flight at a time.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> that completes only once the underlying platform discovery session
    /// has fully started.
    /// </returns>
    Task StartAsync();

    /// <summary>
    /// Cancels the discover loop.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> that completes only once the underlying platform discovery session
    /// has fully stopped.
    /// </returns>
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
    /// <param name="cancellationToken">
    /// A token to stop enumerating the event stream. Cancelling this token detaches the observer
    /// and ends the <see langword="await foreach"/> loop, but does <strong>not</strong> stop the
    /// background discovery loop or the underlying platform operation. To fully stop discovering,
    /// call <see cref="StopAsync"/>.
    /// </param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="DiscovererEvent"/> instances.
    /// </returns>
    /// <remarks>
    /// Multiple concurrent enumerators are supported. Each call to <see cref="EventsAsync"/>
    /// receives its own independent copy of all events via fan-out — a second concurrent caller
    /// does not steal items from the first enumerator.
    /// </remarks>
    IAsyncEnumerable<DiscovererEvent> EventsAsync(CancellationToken cancellationToken = default);
}
