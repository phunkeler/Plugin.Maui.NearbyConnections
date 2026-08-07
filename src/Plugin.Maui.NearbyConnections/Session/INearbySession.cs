using System.Collections.Specialized;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides the entry point to nearby connectivity: advertises this device, discovers others,
/// establishes connections, and exposes every device's lifecycle through a single collection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime.</b> This service is registered as a singleton, because there is one radio and one
/// native session per device. The dependency injection container owns the instance and tears it
/// down. Call <see cref="StopAsync(CancellationToken)"/> to return the session to its initial
/// state rather than disposing it, so that a single page cannot end the session for the whole
/// application.
/// </para>
/// <para>
/// <b>State compared with streams.</b> Device presence and connection state are exposed as state:
/// the observable <see cref="Devices"/> collection and the three lifecycle events. Inbound payloads
/// are instead exposed as a stream, consumed for each connection through
/// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/>. A connection supports a single
/// payload consumer; distribute payloads to multiple components in your own code.
/// </para>
/// <para>
/// <b>Thread safety.</b> Platform callbacks arrive on background threads owned by the underlying
/// platform SDK. The session marshals every <see cref="Devices"/> mutation, every
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> notification, and every lifecycle event
/// onto the UI dispatcher, so event handlers and data bindings require no further marshalling.
/// Handlers run synchronously on the dispatcher; keep them short and perform no I/O in them.
/// </para>
/// <para>
/// <b>Subscription lifetime.</b> The events on this interface live as long as the singleton. A
/// view model that subscribes without unsubscribing remains alive for the lifetime of the
/// application, and navigating to the page a second time adds a second subscription. Always pair
/// an <c>+=</c> subscription with a matching <c>-=</c> when the subscriber has a shorter lifetime
/// than the session.
/// </para>
/// </remarks>
/// <seealso cref="NearbyDevice"/>
/// <seealso cref="NearbyConnection"/>
public interface INearbySession
{
    /// <summary>
    /// Gets the devices known to this session, from first discovery until they are no longer
    /// visible.
    /// </summary>
    /// <value>
    /// A read-only collection of the devices currently known to the session. The collection
    /// implements <see cref="INotifyCollectionChanged"/>.
    /// </value>
    /// <remarks>
    /// A single collection spans the whole device lifecycle; devices do not move between
    /// collections as they connect. <see cref="NearbyDevice.Status"/> indicates the current state,
    /// and each device raises <see cref="INotifyPropertyChanged.PropertyChanged"/> when its state
    /// changes, so a bound item updates in place. Bind directly to this collection, or cast it to
    /// <see cref="INotifyCollectionChanged"/> to observe additions and removals in code. To display
    /// only connected devices, filter on <see cref="NearbyDeviceStatus.Connected"/>.
    /// </remarks>
    IReadOnlyList<NearbyDevice> Devices { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently advertising its presence.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the device is advertising; otherwise, <see langword="false"/>.
    /// </value>
    bool IsAdvertising { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently discovering nearby devices.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the device is discovering; otherwise, <see langword="false"/>.
    /// </value>
    bool IsDiscovering { get; }

    /// <summary>
    /// Occurs when a remote device requests a connection.
    /// </summary>
    /// <remarks>
    /// Respond by calling <see cref="AcceptAsync(NearbyDevice, CancellationToken)"/> or
    /// <see cref="RejectAsync(NearbyDevice, CancellationToken)"/>. The device remains in the
    /// <see cref="NearbyDeviceStatus.RequestReceived"/> state while the request is outstanding.
    /// Leaving a request unanswered holds the remote device in its own pending state until the
    /// platform times the request out.
    /// </remarks>
    event EventHandler<NearbyConnectionRequestedEventArgs> ConnectionRequested;

    /// <summary>
    /// Occurs when a connection to a remote device is established, in either direction.
    /// </summary>
    /// <remarks>
    /// When this event is raised, the device's <see cref="NearbyDevice.Status"/> is
    /// <see cref="NearbyDeviceStatus.Connected"/> and its <see cref="NearbyDevice.Connection"/> is
    /// not <see langword="null"/>. Start consuming payloads at this point by calling
    /// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/>.
    /// </remarks>
    event EventHandler<NearbyConnectionChangedEventArgs> ConnectionEstablished;

    /// <summary>
    /// Occurs when an established connection ends, whether by a local disconnect, a remote
    /// disconnect, or loss of the link.
    /// </summary>
    /// <remarks>
    /// The device returns to <see cref="NearbyDeviceStatus.Visible"/> if it is still in range, and
    /// its <see cref="NearbyDevice.Connection"/> is set to <see langword="null"/>. Any in-flight
    /// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/> enumeration completes on its
    /// own; no cleanup is required for payload consumption.
    /// </remarks>
    event EventHandler<NearbyConnectionChangedEventArgs> ConnectionDropped;

    /// <summary>
    /// Starts advertising this device so that nearby devices can discover and connect to it.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while starting advertising.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when
    /// the platform has started advertising.
    /// </returns>
    /// <remarks>
    /// Inbound connection requests are reported through the <see cref="ConnectionRequested"/>
    /// event. Advertising and discovery are independent; starting one does not affect the other.
    /// Calling this method while the device is already advertising performs no operation.
    /// </remarks>
    /// <exception cref="NearbyAdvertisingException">
    /// The platform failed to start advertising.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising this device.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while stopping advertising.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when
    /// the platform has stopped advertising.
    /// </returns>
    /// <remarks>
    /// Established connections are unaffected, and discovery continues if it was already running.
    /// </remarks>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts discovering nearby devices that are advertising.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while starting discovery.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when
    /// the platform has started discovery.
    /// </returns>
    /// <remarks>
    /// Discovered devices are added to <see cref="Devices"/> with a
    /// <see cref="NearbyDevice.Status"/> of <see cref="NearbyDeviceStatus.Visible"/>. Advertising
    /// and discovery are independent; starting one does not affect the other. Calling this method
    /// while the device is already discovering performs no operation.
    /// </remarks>
    /// <exception cref="NearbyDiscoveryException">
    /// The platform failed to start discovery.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    Task StartDiscoveringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovering nearby devices.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while stopping discovery.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when
    /// the platform has stopped discovery.
    /// </returns>
    /// <remarks>
    /// Established connections are unaffected, and advertising continues if it was already
    /// running. Devices that were only visible are removed from <see cref="Devices"/>; connected
    /// devices remain.
    /// </remarks>
    Task StopDiscoveringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising and discovery and disconnects every established connection, returning the
    /// session to its initial state.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while stopping the session.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when
    /// the session is fully stopped.
    /// </returns>
    /// <remarks>
    /// The session remains usable after this method returns; advertising or discovery can be
    /// started again at any time.
    /// </remarks>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a connection to a discovered device.
    /// </summary>
    /// <param name="device">The device to connect to.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the remote device to accept
    /// the request. Canceling abandons the pending attempt locally, but does not guarantee that
    /// the remote device is notified.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The value of its
    /// <see cref="Task{TResult}.Result"/> property is the established
    /// <see cref="NearbyConnection"/>.
    /// </returns>
    /// <remarks>
    /// While the handshake is in progress, the device's <see cref="NearbyDevice.Status"/> is
    /// <see cref="NearbyDeviceStatus.Connecting"/> and its <see cref="NearbyDevice.Role"/> is
    /// <see cref="ConnectionRole.Initiator"/>; on success the status becomes
    /// <see cref="NearbyDeviceStatus.Connected"/>. The returned connection is the same instance as
    /// <see cref="NearbyDevice.Connection"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The connection could not be established because the remote device rejected the request, the
    /// device is no longer visible, or the platform returned an error.
    /// </exception>
    /// <exception cref="NearbyConnectionTimeoutException">
    /// The remote device did not answer within
    /// <see cref="NearbyConnectionsOptions.InvitationTimeout"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before the connection was established.
    /// </exception>
    Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an outstanding connection request from the specified device.
    /// </summary>
    /// <param name="device">The device whose connection request to accept.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the connection to be
    /// established.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The value of its
    /// <see cref="Task{TResult}.Result"/> property is the established
    /// <see cref="NearbyConnection"/>.
    /// </returns>
    /// <remarks>
    /// Connection requests are reported through the <see cref="ConnectionRequested"/> event. A
    /// request can be accepted only once, and only before it expires.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The device has no outstanding connection request, or the platform failed to complete the
    /// connection.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before the connection was established.
    /// </exception>
    Task<NearbyConnection> AcceptAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an outstanding connection request from the specified device.
    /// </summary>
    /// <param name="device">The device whose connection request to reject.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while signaling the rejection.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when
    /// the rejection has been signaled to the platform.
    /// </returns>
    /// <remarks>
    /// The device returns to the <see cref="NearbyDeviceStatus.Visible"/> state. A request can be
    /// rejected only once, and only before it expires.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The device has no outstanding connection request.
    /// </exception>
    Task RejectAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects an established connection to the specified device, leaving every other
    /// connection intact.
    /// </summary>
    /// <param name="device">The device to disconnect from.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while disconnecting.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when
    /// the connection has been torn down.
    /// </returns>
    /// <remarks>
    /// Disconnecting a device that is not connected performs no operation. The
    /// <see cref="ConnectionDropped"/> event is raised as the connection ends.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);
}
