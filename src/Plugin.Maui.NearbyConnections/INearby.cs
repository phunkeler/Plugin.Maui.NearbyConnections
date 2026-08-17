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
/// state rather than disposing it.
/// </para>
/// <para>
/// <b>State compared with streams.</b> Device presence and connection state are exposed as state:
/// <see cref="Devices"/> is the current set, and <see cref="INearbyDevices.Changes"/> the deltas to
/// it. Inbound payloads are exposed as a stream, consumed for each connection through
/// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/>. A connection supports a single
/// payload consumer; distribute payloads to multiple components in your own code.
/// </para>
/// <para>
/// <b>Thread safety.</b> Every member of this interface is callable from any thread, and nothing
/// here has UI thread affinity. <see cref="INearbyDevices.Changes"/> is delivered on a thread-pool
/// thread, never the UI thread and never the platform SDK's own callback thread: the SDK callback
/// writes into an internal channel, and the pump that drains it publishes the change. Do not rely
/// on the SDK's callback thread or its ordering reaching this stream. A consumer that binds to a
/// user interface marshals for itself — or constructs a <see cref="NearbyDeviceCollection"/>, which
/// is the supported way to get a bindable <c>ObservableCollection</c> back.
/// </para>
/// <para>
/// <b>Subscription lifetime.</b> There is nothing to unsubscribe from. An enumeration of
/// <see cref="INearbyDevices.Changes"/> ends when its cancellation token is cancelled or the loop
/// is exited, so a consumer with a shorter lifetime than this singleton cannot leak the way an
/// undetached event handler could.
/// </para>
/// <para>
/// <b>Platform support.</b> This plugin supports Android and iOS only. On every other target
/// framework (including a plain <c>net10.0</c> class library with no platform suffix), every member
/// that would otherwise reach the platform throws <see cref="PlatformNotSupportedException"/>.
/// <see cref="CheckAvailabilityAsync(CancellationToken)"/> is the exception: it reports
/// <see cref="NearbyAvailability.UnsupportedPlatform"/> instead of throwing, consistent with never
/// throwing to report unavailability.
/// </para>
/// </remarks>
/// <seealso cref="NearbyDevice"/>
/// <seealso cref="NearbyConnection"/>
public interface INearby
{
    /// <summary>
    /// Gets the devices known to this session, from first discovery until they are no longer
    /// visible, together with the stream of changes to that set.
    /// </summary>
    /// <value>
    /// The current set of devices. Enumerating it yields an immutable snapshot, so it is safe to
    /// read from any thread.
    /// </value>
    /// <remarks>
    /// A single collection spans the whole device lifecycle; devices do not move between
    /// collections as they connect. <see cref="NearbyDevice.Status"/> reports where a device is in
    /// that lifecycle. Read this property for the current state and enumerate
    /// <see cref="INearbyDevices.Changes"/> for what happens next — every connection lifecycle
    /// transition arrives there. To display only connected devices, filter on
    /// <see cref="NearbyDeviceStatus.Connected"/>.
    /// </remarks>
    INearbyDevices Devices { get; }

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
    /// Determines whether nearby connectivity can be started, and what is preventing it if not.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while checking.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The value of its
    /// <see cref="Task{TResult}.Result"/> property is <see cref="NearbyAvailability.Ready"/> when
    /// advertising and discovery can be started, or a combination of flags describing what is
    /// missing.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Call this before <see cref="StartAdvertisingAsync(CancellationToken)"/> or
    /// <see cref="StartDiscoveryAsync(CancellationToken)"/> to tell the user what to fix. Without
    /// it, a missing permission or a disabled radio causes advertising and discovery to fail
    /// silently on Android, and to succeed but discover nothing on iOS.
    /// </para>
    /// <para>
    /// This method reports state; it does not change it. It never prompts for permissions and never
    /// enables a radio. Request permissions with the .NET MAUI <c>Permissions</c> API, and direct
    /// the user to system settings to enable a radio.
    /// </para>
    /// <para>
    /// The result is a snapshot. The user can disable a radio or revoke a permission immediately
    /// afterwards, so a <see cref="NearbyAvailability.Ready"/> result is not a guarantee that
    /// starting will succeed.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example checks availability before starting discovery.
    /// <code language="csharp">
    /// var availability = await session.CheckAvailabilityAsync();
    ///
    /// if (availability is not NearbyAvailability.Ready)
    /// {
    ///     if (availability.HasFlag(NearbyAvailability.MissingPermissions))
    ///     {
    ///         // Android needs more than Bluetooth alone: NEARBY_WIFI_DEVICES from API 33, and
    ///         // location below API 31, where Permissions.Bluetooth requests nothing at all.
    ///         await Permissions.RequestAsync&lt;Permissions.Bluetooth&gt;();
    ///
    ///         if (OperatingSystem.IsAndroidVersionAtLeast(33))
    ///         {
    ///             await Permissions.RequestAsync&lt;Permissions.NearbyWifiDevices&gt;();
    ///         }
    ///         else if (!OperatingSystem.IsAndroidVersionAtLeast(31))
    ///         {
    ///             await Permissions.RequestAsync&lt;Permissions.LocationWhenInUse&gt;();
    ///         }
    ///     }
    ///
    ///     if (availability.HasFlag(NearbyAvailability.BluetoothDisabled))
    ///     {
    ///         await Shell.Current.DisplayAlertAsync(
    ///             "Bluetooth is off", "Turn on Bluetooth to find nearby devices.", "OK");
    ///     }
    ///
    ///     return;
    /// }
    ///
    /// await session.StartDiscoveryAsync();
    /// </code>
    /// </example>
    Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

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
    /// An inbound connection request surfaces as a device whose
    /// <see cref="NearbyDevice.Status"/> is <see cref="NearbyDeviceStatus.RequestReceived"/>,
    /// reported through <see cref="INearbyDevices.Changes"/>. Advertising and discovery are
    /// independent; starting one does not affect the other.
    /// Calling this method while the device is already advertising is a no-op.
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
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
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
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);

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
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);

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
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
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
    /// <see cref="NearbyDeviceStatus.Connecting"/> with a <see cref="NearbyDevice.Role"/> of
    /// <see cref="ConnectionRole.Initiator"/>; on success the status becomes
    /// <see cref="NearbyDeviceStatus.Connected"/>. The returned connection is the same instance
    /// that <see cref="TryGetConnection(string, out NearbyConnection)"/> hands back.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NearbyException">
    /// The connection could not be established because the remote device rejected the request, the
    /// device is no longer visible, or the platform returned an error.
    /// </exception>
    /// <exception cref="NearbyConnectionTimeoutException">
    /// The remote device did not answer within
    /// <see cref="NearbyOptions.ConnectTimeout"/>.
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
    /// A device with an outstanding request has a <see cref="NearbyDevice.Status"/> of
    /// <see cref="NearbyDeviceStatus.RequestReceived"/>, reported through
    /// <see cref="INearbyDevices.Changes"/>. A request can be accepted only once, and only before
    /// it expires.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The device has no outstanding connection request.
    /// </exception>
    /// <exception cref="NearbyException">
    /// The platform failed to complete the connection.
    /// </exception>
    /// <exception cref="NearbyConnectionTimeoutException">
    /// The connection was not established within <see cref="NearbyOptions.AcceptTimeout"/> of this
    /// call. That interval is shorter than <see cref="NearbyOptions.ConnectTimeout"/> by default,
    /// because the decision to accept is already made and only the handshake remains — a remote
    /// device that leaves range mid-handshake reports no terminal result on either platform.
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
    /// Gets the established connection to a device, if one exists.
    /// </summary>
    /// <param name="deviceId">The <see cref="NearbyDevice.Id"/> of the device.</param>
    /// <param name="connection">
    /// When this method returns, the established <see cref="NearbyConnection"/> to the device, or
    /// <see langword="null"/> if the device is not connected.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the device is connected; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Safe to call from any thread, like every member of this interface. A connection is held
    /// separately from the device set because a <see cref="NearbyDevice"/> is an immutable snapshot
    /// and cannot carry a live handle. The result is itself a snapshot — a connection can drop
    /// immediately afterwards, so treat a successful lookup as "was connected a moment ago" and
    /// handle the send failing.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="deviceId"/> is <see langword="null"/>.
    /// </exception>
    bool TryGetConnection(string deviceId, [NotNullWhen(true)] out NearbyConnection? connection);

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
    /// Disconnecting a device that is not connected performs no operation. The device returns to
    /// <see cref="NearbyDeviceStatus.Visible"/> as the connection ends, reported through
    /// <see cref="INearbyDevices.Changes"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    Task DisconnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);
}