namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Interface defining the Nearby Connections functionality.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Thread safety.</strong>
/// <see cref="AdvertiseAsync"/> and <see cref="DiscoverAsync"/> are not safe to call concurrently
/// with themselves; start a new session only after the previous one has completed. Concurrent calls
/// to <see cref="ConnectAsync"/> for different <see cref="NearbyDevice"/> instances are safe.
/// <see cref="IAsyncDisposable.DisposeAsync"/> is safe to call from any thread exactly once.
/// </para>
/// <para>
/// Platform callbacks (connection requests, device-found/lost notifications, payload events) are
/// delivered on SDK-owned background threads on both Android and iOS. Consumers must marshal to
/// the UI thread when updating UI from any event yielded by <see cref="AdvertiseAsync"/> or
/// <see cref="DiscoverAsync"/>.
/// </para>
/// <para>
/// <strong>Error delivery.</strong> On both platforms, start failures (advertising or
/// discovery) are delivered asynchronously as a faulted stream — the exception surfaces at
/// the first <c>await</c> of the enumeration, not when <see cref="AdvertiseAsync"/> /
/// <see cref="DiscoverAsync"/> is called. Wrap the <c>await foreach</c> (header and body) in
/// a <c>try/catch</c> to handle start failures.
/// </para>
/// </remarks>
interface IPlatformNearbyConnections : IAsyncDisposable
{
    /// <summary>
    /// Starts advertising this device to nearby discoverers and yields inbound connection requests
    /// as they arrive.
    /// </summary>
    /// <remarks>
    /// Advertising begins when enumeration starts and stops when the enumerable is disposed
    /// (i.e. when the consumer breaks out of the <c>await foreach</c> loop, cancels the token,
    /// or the returned enumerator is otherwise disposed).
    /// Each yielded <see cref="NearbyConnectionRequest"/> represents one inbound connection
    /// attempt. The consumer must call <see cref="NearbyConnectionRequest.AcceptAsync"/> or
    /// <see cref="NearbyConnectionRequest.RejectAsync"/> on every request.
    /// <para>
    /// Items are delivered on a thread-pool thread via an internal channel; marshal to the UI
    /// thread if needed (e.g. <c>await MainThread.InvokeOnMainThreadAsync(...)</c>).
    /// </para>
    /// <para>
    /// <strong>Progress reporting:</strong> outbound file-transfer progress is supplied per-call
    /// via an <c>IProgress&lt;NearbyTransferProgress&gt;?</c> parameter on each
    /// <see cref="NearbyConnection.SendAsync(string, IProgress{NearbyTransferProgress}?, CancellationToken)"/> overload.
    /// Inbound progress is instead a settable property — see <see cref="NearbyConnection.InboundProgress"/>.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">A token to stop advertising and complete the stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyConnectionRequest"/> items,
    /// one per inbound connection request received while advertising.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="NearbyAdvertisingException">Thrown if the platform fails to start advertising. Observed while enumerating the returned stream.</exception>
    IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts discovering nearby advertising devices and yields device-visibility events
    /// as they arrive.
    /// </summary>
    /// <remarks>
    /// Discovery begins when enumeration starts and stops when the enumerable is disposed.
    /// Each yielded <see cref="NearbyDeviceEvent"/> carries a <see cref="NearbyDevice"/> and a
    /// <see cref="NearbyDeviceEventType"/> indicating whether the device was found or lost.
    /// Events for the same device are ordered: a <see cref="NearbyDeviceEventType.Lost"/> event
    /// is always preceded by a corresponding <see cref="NearbyDeviceEventType.Found"/> event.
    /// <para>
    /// Items are delivered on a thread-pool thread via an internal channel; marshal to the UI
    /// thread if needed (e.g. <c>await MainThread.InvokeOnMainThreadAsync(...)</c>).
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">A token to stop discovery and complete the stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyDeviceEvent"/> items.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="NearbyDiscoveryException">Thrown if the platform fails to start discovery. Observed while enumerating the returned stream.</exception>
    IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a connection to the specified <paramref name="device"/> discovered during
    /// <see cref="DiscoverAsync"/> and returns a <see cref="NearbyConnection"/> when the
    /// remote device accepts the connection.
    /// </summary>
    /// <param name="device">The device to connect to. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">
    /// A token to cancel waiting for the remote device to accept the connection. Cancellation
    /// removes the pending connection attempt but does not guarantee the remote device was notified.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to a <see cref="NearbyConnection"/> once
    /// the remote device accepts the request.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection cannot be established. Common causes: the remote device rejected
    /// the connection, the device is no longer visible (not found in the device manager), the
    /// platform returned an error status, or no active session exists on iOS.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled before the connection is established.</exception>
    Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports whether the platform can start advertising or discovery right now, and what is
    /// missing if it cannot. Never prompts and never mutates state.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>A task resolving to the current availability.</returns>
    Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
}
