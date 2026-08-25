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
/// delivered on SDK-owned threads, and this interface makes no guarantee about which. iOS documents
/// its <c>MCSessionDelegate</c> calls as arriving on a private serial queue; Android's GMS Nearby
/// documents no threading contract at all. The implementation therefore records what a callback saw
/// on whatever thread it arrived on, and writes it into a channel.
/// </para>
/// <para>
/// This thread never reaches an application. <see cref="NearbyImplementation"/> drains those
/// channels and republishes on a thread-pool thread, which is what the public
/// <see cref="INearbyDevices.Changes"/> and
/// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/> contracts promise. Do not
/// document the SDK's callback thread as observable to a consumer.
/// </para>
/// <para>
/// <strong>Error delivery.</strong> <see cref="AdvertiseAsync"/> and <see cref="DiscoverAsync"/>
/// each take a <see cref="TaskCompletionSource"/> <c>started</c> that resolves once the platform
/// start phase is known: it completes successfully once advertising/discovery has actually started,
/// or faults with the typed exception if the platform failed to start. Await that task, not the
/// first item of the stream, to learn whether starting succeeded — the stream itself can validly
/// yield nothing for a long time on a successful start. A fault delivered <em>after</em>
/// <c>started</c> has already completed successfully (e.g. the platform's radio drops later)
/// instead ends the stream with that exception; it does not retroactively fault <c>started</c>.
/// </para>
/// <para>
/// <strong>Start latency.</strong> How quickly <c>started</c> resolves on a successful start
/// differs by platform: Android's platform start call is directly awaitable, so <c>started</c>
/// resolves as soon as it returns. iOS has no equivalent success signal — Multipeer Connectivity
/// only reports failure — so a successful iOS start always waits out
/// <see cref="NearbyAppleOptions.StartFailureGraceWindow"/> before <c>started</c> resolves. A
/// consumer timing <c>StartAdvertisingAsync</c>/<c>StartDiscoveryAsync</c>, or asserting against a
/// tight deadline in a test, should expect iOS to take measurably longer than Android in the
/// success case.
/// </para>
/// </remarks>
interface IPlatformNearby : IAsyncDisposable
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
    /// thread if needed — in .NET MAUI, an injected <see cref="IDispatcher"/>
    /// (<see cref="IDispatcher.Dispatch(Action)"/>), which is what
    /// <see cref="NearbyDeviceCollection{TRow}"/> is given.
    /// </para>
    /// <para>
    /// <strong>Progress reporting:</strong> outbound file-transfer progress is supplied per-call
    /// via an <c>IProgress&lt;NearbyTransferProgress&gt;?</c> parameter on each
    /// <see cref="NearbyConnection.SendAsync(string, IProgress{NearbyTransferProgress}?, CancellationToken)"/> overload.
    /// Inbound progress is instead a settable property — see <see cref="NearbyConnection.InboundProgress"/>.
    /// </para>
    /// </remarks>
    /// <param name="started">
    /// Resolved once the platform start phase is known — see the interface-level <b>Error
    /// delivery</b> remarks.
    /// </param>
    /// <param name="cancellationToken">A token to stop advertising and complete the stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyConnectionRequest"/> items,
    /// one per inbound connection request received while advertising.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="NearbyAdvertisingException">Thrown if the platform fails to start advertising. Delivered through <paramref name="started"/>.</exception>
    IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(TaskCompletionSource started, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts discovering nearby advertising devices and yields device-visibility events
    /// as they arrive.
    /// </summary>
    /// <remarks>
    /// Discovery begins when enumeration starts and stops when the enumerable is disposed.
    /// Each yielded <see cref="NearbyDeviceEvent"/> carries a <see cref="NearbyDevice"/> and a
    /// <see cref="NearbyDeviceEvent.Found"/> flag indicating whether the device was found or lost.
    /// Events for the same device are ordered: a lost event is always preceded by a corresponding
    /// found event.
    /// <para>
    /// Items are delivered on a thread-pool thread via an internal channel; marshal to the UI
    /// thread if needed — in .NET MAUI, an injected <see cref="IDispatcher"/>
    /// (<see cref="IDispatcher.Dispatch(Action)"/>), which is what
    /// <see cref="NearbyDeviceCollection{TRow}"/> is given.
    /// </para>
    /// </remarks>
    /// <param name="started">
    /// Resolved once the platform start phase is known — see the interface-level <b>Error
    /// delivery</b> remarks.
    /// </param>
    /// <param name="cancellationToken">A token to stop discovery and complete the stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyDeviceEvent"/> items.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="NearbyDiscoveryException">Thrown if the platform fails to start discovery. Delivered through <paramref name="started"/>.</exception>
    IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(TaskCompletionSource started, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Looks up the live connection for <paramref name="deviceId"/>, if the platform holds one.
    /// </summary>
    /// <remarks>
    /// The platform's connection table is the one owner of the fact "device X has a live
    /// connection" — see the C5 table in <c>docs/ARCHITECTURE.md</c> section 4. The session
    /// queries it here instead of keeping a second table of its own.
    /// </remarks>
    /// <param name="deviceId">The device id minted by this library.</param>
    /// <param name="connection">The live connection, when one exists.</param>
    /// <returns><see langword="true"/> when the platform holds a live connection for the device.</returns>
    bool TryGetConnection(string deviceId, [NotNullWhen(true)] out NearbyConnection? connection);

    /// <summary>
    /// Snapshots the live connections at the moment of the call.
    /// </summary>
    /// <remarks>
    /// The array is a copy: it does not track later opens or releases. This is the read path for
    /// teardown, and later for the delivery replay set (contract C3).
    /// </remarks>
    /// <returns>The live connections, possibly empty.</returns>
    NearbyConnection[] SnapshotConnections();
}