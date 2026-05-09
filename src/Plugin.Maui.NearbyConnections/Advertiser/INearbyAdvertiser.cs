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
    /// Gets the inbound connection requests that are awaiting acceptance or rejection.
    /// </summary>
    IReadOnlyList<NearbyConnectionRequest> PendingRequests { get; }

    /// <summary>
    /// Gets the currently live connections accepted by this advertiser.
    /// </summary>
    IReadOnlyList<NearbyConnection> ActiveConnections { get; }

    /// <summary>
    /// Starts the background advertise loop. Returns <see cref="Task.CompletedTask"/> once the loop
    /// is launched (fire-and-forget internally).
    /// </summary>
    /// <param name="cancellationToken">
    /// An optional token that, when cancelled, will also stop the advertise loop.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes once the loop has been started.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the advertise loop and returns immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes immediately after the loop is signalled to stop.</returns>
    Task StopAsync();

    /// <summary>
    /// Accepts a pending connection request, moves it from <see cref="PendingRequests"/> to
    /// <see cref="ActiveConnections"/>, and starts monitoring and payload forwarding for
    /// the resulting connection.
    /// </summary>
    /// <param name="request">The request to accept. Must be present in <see cref="PendingRequests"/>.</param>
    /// <param name="cancellationToken">A token to cancel the accept operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to the accepted <see cref="NearbyConnection"/>.
    /// </returns>
    Task<NearbyConnection> AcceptAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending connection request and removes it from <see cref="PendingRequests"/>.
    /// </summary>
    /// <param name="request">The request to reject. Must be present in <see cref="PendingRequests"/>.</param>
    /// <param name="cancellationToken">A token to cancel the reject operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the rejection has been signalled to the platform.</returns>
    Task RejectAsync(NearbyConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a unified payload stream across all active connections managed by this advertiser.
    /// The stream exits when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// Do not call <see cref="NearbyConnection.ReceiveAsync"/> on a connection returned by this
    /// advertiser while also consuming <see cref="ReceiveAllAsync"/>. Both paths read from the same
    /// <c>SingleReader</c> channel and will corrupt each other's streams.
    /// </remarks>
    /// <param name="cancellationToken">A token to stop enumerating the unified stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of tuples pairing each
    /// <see cref="NearbyConnection"/> with the <see cref="NearbyPayload"/> it received.
    /// </returns>
    IAsyncEnumerable<(NearbyConnection Connection, NearbyPayload Payload)> ReceiveAllAsync(CancellationToken cancellationToken = default);
}
