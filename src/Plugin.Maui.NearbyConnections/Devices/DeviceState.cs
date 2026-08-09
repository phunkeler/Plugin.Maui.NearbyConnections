namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The position of a <see cref="NearbyDevice"/> in its lifecycle, together with the data that is
/// meaningful only in that position.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed hierarchy: the base constructor is private, so the four nested cases below are
/// the only possible states. Consumers pattern-match rather than construct.
/// </para>
/// <para>
/// State is the single settable fact on a device. <see cref="NearbyDevice.Status"/> is a projection
/// of it, kept for consumers that only need the coarse question answered. The role a device plays
/// and the connection it holds live on the cases that actually have them, so an unconnected device
/// cannot carry a connection and a connected one cannot lack a role.
/// </para>
/// </remarks>
/// <example>
/// The following example reads the connection from a device's state.
/// <code language="csharp">
/// if (device.State is DeviceState.Connected { Connection: var connection })
/// {
///     await connection.SendAsync(payload, cancellationToken);
/// }
/// </code>
/// </example>
public abstract record DeviceState
{
    // Private so the hierarchy stays closed: only the nested records below can derive from it.
    DeviceState()
    {
    }

    /// <summary>
    /// The device has been discovered and is in range, with no connection negotiation in progress.
    /// This is the initial state for a discovered device, and the state a device returns to after a
    /// connection ends or a request is rejected.
    /// </summary>
    public sealed record Visible : DeviceState;

    /// <summary>
    /// The device has requested a connection and is awaiting a response. Accept or reject the
    /// request to leave this state.
    /// </summary>
    /// <remarks>
    /// No role is carried here. The local device is not yet an acceptor — it becomes one only when
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> is called.
    /// </remarks>
    public sealed record RequestReceived : DeviceState;

    /// <summary>
    /// A connection handshake is in progress, in either direction.
    /// </summary>
    /// <param name="Role">The role the local device plays in the handshake.</param>
    /// <remarks>
    /// This state is advisory and is not a guaranteed step in the lifecycle. On iOS, a device can
    /// move from an invitation directly to a disconnected state without ever being observed in this
    /// state, both when an invitation is declined and, less commonly, on error. Do not treat this
    /// state as a precondition for clearing pending connection state.
    /// </remarks>
    public sealed record Connecting(ConnectionRole Role) : DeviceState;

    /// <summary>
    /// A connection is established.
    /// </summary>
    /// <param name="Role">The role the local device played in establishing the connection.</param>
    /// <param name="Connection">The active connection to the device.</param>
    public sealed record Connected(ConnectionRole Role, NearbyConnection Connection) : DeviceState;
}
