namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Interface defining the Nearby Connections functionality.
/// </summary>
public interface INearbyConnections : IDisposable
{
    /// <summary>
    /// Gets a read-only snapshot of all currently tracked nearby devices and their connection states.
    /// </summary>
    IReadOnlyList<NearbyDevice> Devices { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently advertising to nearby devices.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently discovering nearby devices.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Starts advertising this device to nearby discoverers.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation of starting advertising.</returns>
    /// <exception cref="NearbyAdvertisingException">Thrown if advertising fails to start.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start discovering nearby devices.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation of starting discovery.</returns>
    /// <exception cref="NearbyDiscoveryException">Thrown if discovery fails to start.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop advertising this device.
    /// </summary>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop discovering nearby devices.
    /// </summary>
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from a connected nearby device.
    /// </summary>
    /// <param name="device">The device to disconnect from. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous disconnect operation.</returns>
    /// <remarks>
    /// On Android, background connectivity requires a foreground service. Call this method
    /// when your app is backgrounded without a foreground service to allow clean reconnection
    /// when the app resumes.
    /// </remarks>
    Task DisconnectAsync(NearbyDevice device);

    /// <summary>
    /// Send an invitation to connect to the specified <see cref="NearbyDevice"/>.
    /// </summary>
    /// <param name="device">The device to which the invitation will be sent. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation of sending the invitation.</returns>
    Task RequestConnectionAsync(NearbyDevice device);

    /// <summary>
    /// Respond to a connection request from the specified <see cref="NearbyDevice"/>.
    /// </summary>
    /// <param name="device">The device that sent the connection request.</param>
    /// <param name="accept"><see langword="true"/> to accept the connection; <see langword="false"/> to decline.</param>
    /// <returns>A task that represents the asynchronous operation of responding to the connection request.</returns>
    Task RespondToConnectionAsync(NearbyDevice device, bool accept);

    /// <summary>
    /// Sends bytes to a connected nearby device.
    /// </summary>
    /// <param name="device">The connected device to send bytes to.</param>
    /// <param name="data">The bytes to send (≤32 KB on Android).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the bytes have been handed off to the platform.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> or <paramref name="device"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the device is not in the <see cref="NearbyDeviceState.Connected"/> state.</exception>
    Task SendAsync(
        NearbyDevice device,
        byte[] data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the contents of URI to a connected nearby device.
    /// </summary>
    /// <param name="device">The connected device to send the resource to.</param>
    /// <param name="uri">
    /// A URI string identifying the resource to send.
    /// </param>
    /// <param name="progress">
    /// An optional callback to receive outgoing transfer progress updates.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the transfer is fully enqueued or finished.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the device is not in the <see cref="NearbyDeviceState.Connected"/> state.
    /// </exception>
    Task SendAsync(
        NearbyDevice device,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when the advertising state changes.
    /// </summary>
    event EventHandler<AdvertisingStateChangedEventArgs>? AdvertisingStateChanged;

    /// <summary>
    /// Raised when the discovering state changes.
    /// </summary>
    event EventHandler<DiscoveringStateChangedEventArgs>? DiscoveringStateChanged;

    /// <summary>
    /// Raised when a nearby device is discovered.
    /// </summary>
    event EventHandler<DeviceFoundEventArgs>? DeviceFound;

    /// <summary>
    /// Raised when a previously discovered nearby device is no longer visible.
    /// </summary>
    event EventHandler<DeviceLostEventArgs>? DeviceLost;

    /// <summary>
    /// Raised when a connected nearby device disconnects.
    /// </summary>
    event EventHandler<DeviceDisconnectedEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Raised when an inbound connection request is received from a nearby device.
    /// </summary>
    event EventHandler<ConnectionRequestedEventArgs>? ConnectionRequested;

    /// <summary>
    /// Raised when a connection request is accepted or rejected by the remote device.
    /// </summary>
    event EventHandler<NearbyDeviceRespondedEventArgs>? ConnectionResponded;

    /// <summary>
    /// Raised for errors that originate inside platform callbacks that the caller cannot catch.
    /// </summary>
    /// <remarks>
    /// This library uses two error-surfacing strategies:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <b>Exceptions</b> — thrown from methods the caller directly invokes (e.g.
    ///       <c>SendAsync</c>, <c>RequestConnectionAsync</c>). The caller is responsible
    ///       for catching these.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b><see cref="ErrorOccurred"/></b> — raised for errors that originate inside
    ///       platform callbacks (e.g. advertising/discovery failures, receive errors).
    ///       Because these fire on platform threads outside the caller's call stack,
    ///       exceptions cannot be caught by the caller, so the error is surfaced here instead.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    event EventHandler<NearbyConnectionsErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Raised when the connection state of a nearby device changes.
    /// </summary>
    event EventHandler<NearbyDeviceStateChangedEventArgs>? DeviceStateChanged;

    /// <summary>
    /// Raised when data is received from a connected nearby device.
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Raised to report progress on an incoming transfer.
    /// Not raised for <see cref="BytesPayload"/> transfers, which complete atomically.
    /// </summary>
    event EventHandler<DataTransferProgressEventArgs>? IncomingTransferProgress;
}