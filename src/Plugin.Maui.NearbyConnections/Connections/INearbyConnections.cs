namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Interface defining the Nearby Connections functionality.
/// </summary>
public interface INearbyConnections : IAsyncDisposable
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
    /// Items are delivered on the platform SDK callback thread, not the main thread.
    /// Marshal to the UI thread yourself if needed (e.g. <c>await MainThread.InvokeOnMainThreadAsync(...)</c>).
    /// </remarks>
    /// <param name="cancellationToken">A token to stop advertising and complete the stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyConnectionRequest"/> items,
    /// one per inbound connection request received while advertising.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
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
    /// Items are delivered on the platform SDK callback thread, not the main thread.
    /// Marshal to the UI thread yourself if needed (e.g. <c>await MainThread.InvokeOnMainThreadAsync(...)</c>).
    /// </remarks>
    /// <param name="cancellationToken">A token to stop discovery and complete the stream.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="NearbyDeviceEvent"/> items.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
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
    /// <exception cref="InvalidOperationException">Thrown if the connection attempt is rejected by the remote device.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled before the connection is established.</exception>
    Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default);
}
