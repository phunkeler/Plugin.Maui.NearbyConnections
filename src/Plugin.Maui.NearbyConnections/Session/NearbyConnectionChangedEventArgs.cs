namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="INearbyConnections.ConnectionEstablished"/> and
/// <see cref="INearbyConnections.ConnectionDropped"/> events.
/// </summary>
public sealed class NearbyConnectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="device">The remote device on the other end of the connection.</param>
    /// <param name="connection">The connection that was established or dropped.</param>
    /// <param name="reason">
    /// Why the connection ended. Defaults to <see cref="EndReason.Unknown"/>, which is the correct
    /// value for <see cref="INearbyConnections.ConnectionEstablished"/>, where nothing has ended.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> or <paramref name="connection"/> is <see langword="null"/>.
    /// </exception>
    public NearbyConnectionChangedEventArgs(
        NearbyDevice device,
        NearbyConnection connection,
        EndReason reason = EndReason.Unknown)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(connection);

        Device = device;
        Connection = connection;
        Reason = reason;
    }

    /// <summary>
    /// Gets the remote device on the other end of the connection.
    /// </summary>
    /// <value>The remote device associated with the connection.</value>
    public NearbyDevice Device { get; }

    /// <summary>
    /// Gets the connection that was established or dropped.
    /// </summary>
    /// <value>The connection associated with the event.</value>
    /// <remarks>
    /// For the <see cref="INearbyConnections.ConnectionDropped"/> event, the connection is already torn
    /// down and cannot be used to send data. It is supplied so that handlers can correlate the
    /// event with the connection instance they were using. The
    /// <see cref="NearbyDevice.State"/> of a dropped device is
    /// <see cref="DeviceState.Visible"/>.
    /// </remarks>
    public NearbyConnection Connection { get; }

    /// <summary>
    /// Gets the reason the connection ended.
    /// </summary>
    /// <value>
    /// One of the <see cref="EndReason"/> values. Always <see cref="EndReason.Unknown"/> for
    /// <see cref="INearbyConnections.ConnectionEstablished"/>, where nothing has ended.
    /// </value>
    /// <remarks>
    /// This is the only place the reason is reported. A device that has dropped is back in
    /// <see cref="DeviceState.Visible"/>, which carries no reason, so a handler that needs to know
    /// why must read it here rather than from the device.
    /// </remarks>
    public EndReason Reason { get; }
}
