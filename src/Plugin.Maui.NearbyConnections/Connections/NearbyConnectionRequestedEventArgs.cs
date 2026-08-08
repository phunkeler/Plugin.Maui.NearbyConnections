namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="INearbyConnections.ConnectionRequested"/> event.
/// </summary>
/// <remarks>
/// Respond to the request by calling
/// <see cref="INearbyConnections.AcceptAsync(NearbyDevice, CancellationToken)"/> or
/// <see cref="INearbyConnections.RejectAsync(NearbyDevice, CancellationToken)"/>. To accept every
/// request automatically, call <see cref="INearbyConnections.AcceptAsync(NearbyDevice, CancellationToken)"/>
/// from the event handler; consider prompting the user before doing so.
/// </remarks>
public sealed class NearbyConnectionRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="device">The device requesting the connection.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> is <see langword="null"/>.
    /// </exception>
    public NearbyConnectionRequestedEventArgs(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        Device = device;
    }

    /// <summary>
    /// Gets the device requesting the connection.
    /// </summary>
    /// <value>The device that sent the connection request.</value>
    /// <remarks>
    /// The device's <see cref="NearbyDevice.Status"/> is
    /// <see cref="NearbyDeviceStatus.RequestReceived"/> until the request is answered.
    /// </remarks>
    public NearbyDevice Device { get; }
}
