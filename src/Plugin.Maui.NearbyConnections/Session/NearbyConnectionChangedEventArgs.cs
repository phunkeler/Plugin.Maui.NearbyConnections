namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Carries the device and connection for a connection that has just been established or dropped.
/// </summary>
public sealed class NearbyConnectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new <see cref="NearbyConnectionChangedEventArgs"/>.
    /// </summary>
    /// <param name="device">The remote device.</param>
    /// <param name="connection">The connection that was established or dropped.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> or <paramref name="connection"/> is <see langword="null"/>.</exception>
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
    public NearbyDevice Device { get; }

    /// <summary>
    /// Gets the connection that was established or dropped.
    /// </summary>
    /// <remarks>
    /// On <see cref="INearbySession.ConnectionDropped"/> the connection is already torn down: it is
    /// supplied so handlers can correlate with the instance they were using, not to send on. A
    /// dropped device's <see cref="NearbyDevice.Connection"/> is <see langword="null"/>.
    /// </remarks>
    public NearbyConnection Connection { get; }
}
