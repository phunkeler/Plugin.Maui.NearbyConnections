using System.Collections.Specialized;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The single entry point to nearby connectivity: advertise this device, discover others, connect,
/// and observe every device's lifecycle through one collection.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Lifetime.</strong> Registered as a singleton — there is one radio, one native session.
/// The container owns it and tears it down; consumers use <see cref="StopAsync"/> rather than
/// disposing it, so a single page cannot end the session app-wide.
/// </para>
/// <para>
/// <strong>State versus streams.</strong> Device presence and connection state are *state*, exposed
/// as the observable <see cref="Devices"/> collection plus three lifecycle events. Inbound payloads
/// are a *stream*, consumed per-connection via
/// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/> — one consumer per connection, fan
/// out above the plugin. See <c>docs/PAYLOAD-DELIVERY.md</c>.
/// </para>
/// <para>
/// <strong>Threading.</strong> Platform callbacks arrive on SDK-owned background threads. The session
/// marshals every <see cref="Devices"/> mutation, <see cref="INotifyPropertyChanged"/> raise, and
/// lifecycle event onto the UI dispatcher, so handlers and bindings are safe without further
/// marshalling. Handlers run synchronously on the dispatcher: keep them fast and do no I/O in them.
/// </para>
/// <para>
/// <strong>Subscription lifetime.</strong> These events live as long as the singleton. A page
/// ViewModel that subscribes without unsubscribing leaks for the life of the app, and re-navigating
/// adds a second subscription. Always pair <c>+=</c> with <c>-=</c>; the sample's
/// <c>BasePageViewModel.RegisterSessionSubscription</c> shows the pattern.
/// </para>
/// </remarks>
public interface INearbySession
{
    /// <summary>
    /// Gets every device known to this session, from first discovery until it is no longer visible.
    /// </summary>
    /// <remarks>
    /// One collection spans the whole lifecycle — devices do not move between collections as they
    /// connect. <see cref="NearbyDevice.Status"/> carries the state, and each device raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> as it changes, so a bound row updates in
    /// place. The collection implements <see cref="INotifyCollectionChanged"/>; cast to it, or bind
    /// directly, to observe additions and removals. To show only connected devices, filter on
    /// <see cref="NearbyDeviceStatus.Connected"/>.
    /// </remarks>
    IReadOnlyList<NearbyDevice> Devices { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently advertising its presence.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently discovering nearby devices.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Raised when a remote device asks to connect. Respond with
    /// <see cref="AcceptAsync"/> or <see cref="RejectAsync"/>.
    /// </summary>
    /// <remarks>
    /// The device is in <see cref="NearbyDeviceStatus.RequestReceived"/> while the request is
    /// outstanding. Leaving a request unanswered holds the remote device in its own pending state
    /// until the platform times it out.
    /// </remarks>
    event EventHandler<NearbyConnectionRequestedEventArgs> ConnectionRequested;

    /// <summary>
    /// Raised when a connection to a remote device is established, in either direction.
    /// </summary>
    /// <remarks>
    /// By the time this is raised, the device's <see cref="NearbyDevice.Status"/> is
    /// <see cref="NearbyDeviceStatus.Connected"/> and <see cref="NearbyDevice.Connection"/> is
    /// non-<see langword="null"/>. This is the point at which to start consuming payloads with
    /// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/>.
    /// </remarks>
    event EventHandler<NearbyConnectionChangedEventArgs> ConnectionEstablished;

    /// <summary>
    /// Raised when an established connection ends, whether by local disconnect, remote disconnect,
    /// or loss of the link.
    /// </summary>
    /// <remarks>
    /// The device returns to <see cref="NearbyDeviceStatus.Visible"/> if still in range, and its
    /// <see cref="NearbyDevice.Connection"/> is cleared. Any in-flight
    /// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/> loop completes on its own — no
    /// cleanup is required for payload consumption.
    /// </remarks>
    event EventHandler<NearbyConnectionChangedEventArgs> ConnectionDropped;

    /// <summary>
    /// Starts advertising this device so nearby discoverers can find and connect to it.
    /// Inbound requests arrive as <see cref="ConnectionRequested"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel starting advertising.</param>
    /// <returns>A task that completes once the platform has started advertising.</returns>
    /// <remarks>
    /// Advertising and discovery are independent — starting one does not affect the other. Calling
    /// this while already advertising is a no-op.
    /// </remarks>
    /// <exception cref="NearbyAdvertisingException">Thrown if the platform fails to start advertising.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising this device. Established connections are unaffected, and discovery
    /// continues if it was running.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel stopping advertising.</param>
    /// <returns>A task that completes once the platform has stopped advertising.</returns>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts discovering nearby advertising devices. Discovered devices appear in
    /// <see cref="Devices"/> with <see cref="NearbyDeviceStatus.Visible"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel starting discovery.</param>
    /// <returns>A task that completes once the platform has started discovering.</returns>
    /// <remarks>
    /// Advertising and discovery are independent — starting one does not affect the other. Calling
    /// this while already discovering is a no-op.
    /// </remarks>
    /// <exception cref="NearbyDiscoveryException">Thrown if the platform fails to start discovery.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
    Task StartDiscoveringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovering nearby devices. Established connections are unaffected, and advertising
    /// continues if it was running.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel stopping discovery.</param>
    /// <returns>A task that completes once the platform has stopped discovering.</returns>
    /// <remarks>
    /// Devices that were merely visible are removed from <see cref="Devices"/>; connected devices
    /// remain.
    /// </remarks>
    Task StopDiscoveringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising and discovery and disconnects every established connection, returning the
    /// session to its initial state. The session remains usable — start again at any time.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the teardown.</param>
    /// <returns>A task that completes once the session is fully stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a connection to a discovered <paramref name="device"/>, completing when the remote
    /// device accepts.
    /// </summary>
    /// <param name="device">The device to connect to.</param>
    /// <param name="cancellationToken">
    /// A token to stop waiting for the remote device to accept. Cancellation abandons the pending
    /// attempt locally but does not guarantee the remote device was notified.
    /// </param>
    /// <returns>A task that resolves to the established <see cref="NearbyConnection"/>.</returns>
    /// <remarks>
    /// The device moves to <see cref="NearbyDeviceStatus.Connecting"/> with
    /// <see cref="ConnectionRole.Initiator"/> while the handshake is in flight, then to
    /// <see cref="NearbyDeviceStatus.Connected"/>. The returned connection is the same instance as
    /// <see cref="NearbyDevice.Connection"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection cannot be established — the remote device rejected it, the device
    /// is no longer visible, or the platform returned an error.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled before the connection is established.</exception>
    Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an outstanding connection request from <paramref name="device"/>, reported by
    /// <see cref="ConnectionRequested"/>.
    /// </summary>
    /// <param name="device">The device whose request to accept.</param>
    /// <param name="cancellationToken">A token to cancel waiting for the connection to be established.</param>
    /// <returns>A task that resolves to the established <see cref="NearbyConnection"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the device has no outstanding request, or the platform fails to complete the connection.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled before the connection is established.</exception>
    Task<NearbyConnection> AcceptAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an outstanding connection request from <paramref name="device"/>. The device returns
    /// to <see cref="NearbyDeviceStatus.Visible"/>.
    /// </summary>
    /// <param name="device">The device whose request to reject.</param>
    /// <param name="cancellationToken">A token to cancel signalling the rejection.</param>
    /// <returns>A task that completes once the rejection has been signalled to the platform.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the device has no outstanding request.</exception>
    Task RejectAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects an established connection to <paramref name="device"/>, leaving every other
    /// connection intact.
    /// </summary>
    /// <param name="device">The device to disconnect from.</param>
    /// <param name="cancellationToken">A token to cancel the disconnect.</param>
    /// <returns>A task that completes once the connection has been torn down.</returns>
    /// <remarks>
    /// Disconnecting a device that is not connected is a no-op. <see cref="ConnectionDropped"/> is
    /// raised as the connection ends.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);
}
