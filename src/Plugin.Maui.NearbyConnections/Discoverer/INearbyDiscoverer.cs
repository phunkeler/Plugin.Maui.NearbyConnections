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
    /// Gets the devices that are currently visible (found but not yet connected or lost).
    /// The underlying implementation uses <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>,
    /// enabling XAML collection bindings at runtime.
    /// </summary>
    IReadOnlyList<NearbyDevice> NearbyDevices { get; }

    /// <summary>
    /// Gets the currently live connections initiated by this discoverer.
    /// The underlying implementation uses <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>,
    /// enabling XAML collection bindings at runtime.
    /// </summary>
    IReadOnlyList<NearbyConnection> ActiveConnections { get; }

    /// <summary>
    /// Starts the background discover loop. Returns <see cref="Task.CompletedTask"/> once the loop
    /// is launched (fire-and-forget internally).
    /// </summary>
    /// <param name="cancellationToken">
    /// An optional token that, when cancelled, will also stop the discover loop.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes once the loop has been started.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the discover loop and returns immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes immediately after the loop is signalled to stop.</returns>
    Task StopAsync();

    /// <summary>
    /// Removes <paramref name="device"/> from <see cref="NearbyDevices"/>, delegates to
    /// <see cref="INearbyConnections.ConnectAsync"/>, adds the returned connection to
    /// <see cref="ActiveConnections"/>, and starts monitoring and payload forwarding.
    /// </summary>
    /// <remarks>
    /// If the underlying <see cref="INearbyConnections.ConnectAsync"/> call throws, the device
    /// is not re-added to <see cref="NearbyDevices"/>. The platform may re-fire a
    /// <c>FoundPeer</c> event that restores it — this is a known behavior.
    /// </remarks>
    /// <param name="device">The device to connect to. Must be present in <see cref="NearbyDevices"/>.</param>
    /// <param name="cancellationToken">A token to cancel the connect operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to the established <see cref="NearbyConnection"/>.
    /// </returns>
    Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a unified payload stream across all active connections managed by this discoverer.
    /// The stream exits when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// Do not call <see cref="NearbyConnection.ReceiveAsync"/> on a connection returned by this
    /// discoverer while also consuming <see cref="ReceiveAllAsync"/>. Both paths read from the same
    /// <c>SingleReader</c> channel and will corrupt each other's streams.
    /// </remarks>
    /// <param name="cancellationToken">A token to stop enumerating the unified stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of tuples pairing each
    /// <see cref="NearbyConnection"/> with the <see cref="NearbyPayload"/> it received.
    /// </returns>
    IAsyncEnumerable<(NearbyConnection Connection, NearbyPayload Payload)> ReceiveAllAsync(CancellationToken cancellationToken = default);
}
