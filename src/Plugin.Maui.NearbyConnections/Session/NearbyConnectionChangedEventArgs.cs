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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="device"/> or <paramref name="connection"/> is <see langword="null"/>.
    /// </exception>
    public NearbyConnectionChangedEventArgs(NearbyDevice device, NearbyConnection connection)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(connection);

        Device = device;
        Connection = connection;
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
    /// <see cref="NearbyDevice.Connection"/> property of a dropped device is
    /// <see langword="null"/>.
    /// </remarks>
    public NearbyConnection Connection { get; }
}
