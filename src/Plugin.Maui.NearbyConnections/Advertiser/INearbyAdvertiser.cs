namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A Tier-2 advertiser service that manages the advertising lifecycle, pending connection
/// requests, and active connections on the advertiser side.
/// </summary>
public interface INearbyAdvertiser
{
    /// <summary>
    /// Gets a value indicating whether the advertise loop is currently running.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Starts the background advertise loop. Returns <see cref="Task.CompletedTask"/> once the loop
    /// is launched (fire-and-forget internally).
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes once the loop has been started.</returns>
    Task StartAsync();

    /// <summary>
    /// Cancels the advertise loop and returns immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes immediately after the loop is signalled to stop.</returns>
    Task StopAsync();

    /// <summary>
    /// Accepts a pending connection request and starts monitoring and payload forwarding for
    /// the resulting connection.
    /// </summary>
    /// <param name="request">The request to accept.</param>
    /// <param name="cancellationToken">A token to cancel the accept operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to the accepted <see cref="NearbyConnection"/>.
    /// </returns>
    Task<NearbyConnection> AcceptAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending connection request.
    /// </summary>
    /// <param name="request">The request to reject.</param>
    /// <param name="cancellationToken">A token to cancel the reject operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the rejection has been signalled to the platform.</returns>
    Task RejectAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a unified stream of connection lifecycle and payload events.
    /// The first batch of items reflects current state (pending requests and active connections)
    /// as synthetic events, followed by live events as they occur.
    /// Completes when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">A token to stop enumerating the unified stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="AdvertiserEvent"/> instances.
    /// </returns>
    IAsyncEnumerable<AdvertiserEvent> EventsAsync(CancellationToken cancellationToken = default);
}
