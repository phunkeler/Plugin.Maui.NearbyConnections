namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A Tier-2 advertiser service that manages the advertising lifecycle, pending connection
/// requests, and active connections on the advertiser side.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Shutdown sequence.</strong> To fully stop advertising and release resources, call
/// <see cref="StopAsync"/> to cancel the platform operation and emit any cleanup events, then
/// dispose the instance. <see cref="StopAsync"/> does <strong>not</strong> complete subscriber
/// channels — only <see cref="IAsyncDisposable.DisposeAsync"/> (or <see cref="IDisposable.Dispose"/>)
/// completes subscriber channels, which causes any active <see langword="await foreach"/> loop
/// over <see cref="EventsAsync"/> to drain and exit naturally.
/// </para>
/// <para>
/// Cancelling the <c>cancellationToken</c> passed to <see cref="EventsAsync"/> only detaches the
/// current observer — the background advertising loop and the underlying platform operation
/// continue running until <see cref="StopAsync"/> is called. Use the pattern below for clean
/// teardown:
/// </para>
/// <code>
/// await advertiser.StartAsync();
/// await foreach (var ev in advertiser.EventsAsync())
/// {
///     // handle ev
/// }
/// // When done:
/// await advertiser.StopAsync();  // cancels platform operation, emits cleanup events
/// await advertiser.DisposeAsync(); // completes channels → foreach exits
/// </code>
/// </remarks>
public interface INearbyAdvertiser : IDisposable, IAsyncDisposable
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
    /// <param name="cancellationToken">
    /// A token to stop enumerating the event stream. Cancelling this token detaches the observer
    /// and ends the <see langword="await foreach"/> loop, but does <strong>not</strong> stop the
    /// background advertising loop or the underlying platform operation. To fully stop advertising,
    /// call <see cref="StopAsync"/>.
    /// </param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="AdvertiserEvent"/> instances.
    /// </returns>
    /// <remarks>
    /// Multiple concurrent enumerators are supported. Each call to <see cref="EventsAsync"/>
    /// receives its own independent copy of all events via fan-out — a second concurrent caller
    /// does not steal items from the first enumerator.
    /// </remarks>
    IAsyncEnumerable<AdvertiserEvent> EventsAsync(CancellationToken cancellationToken = default);
}
