namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies where a <see cref="NearbyDevice"/> sits in its lifecycle, from first discovery through
/// to an established connection.
/// </summary>
/// <remarks>
/// A device stays in <see cref="INearby.Devices"/> across every one of these states — the status on
/// the device changes, the device does not move between collections.
/// </remarks>
public enum NearbyDeviceStatus
{
    /// <summary>
    /// The device is in range with no connection negotiation in progress.
    /// </summary>
    /// <remarks>
    /// The initial state for a newly discovered device, and the state a device returns to once a
    /// connection ends or a request is rejected.
    /// </remarks>
    Visible,

    /// <summary>
    /// The device has requested a connection and is awaiting a response.
    /// </summary>
    /// <remarks>
    /// Accepting or rejecting the request is what moves the device out of this state.
    /// </remarks>
    RequestReceived,

    /// <summary>
    /// A connection handshake is in progress. Read <see cref="NearbyDevice.Role"/> to determine
    /// which side initiated it.
    /// </summary>
    /// <remarks>
    /// This state is advisory, not a guaranteed step in the lifecycle: on iOS, a device can move
    /// straight from an invitation to a disconnected state without ever being observed here, both
    /// when the invitation is declined and, less commonly, on error. Do not treat reaching this
    /// state as a precondition for clearing pending connection state.
    /// </remarks>
    Connecting,

    /// <summary>
    /// A connection is established.
    /// </summary>
    /// <remarks>
    /// <see cref="INearby.TryGetConnection(string, out NearbyConnection)"/> returns a connection for
    /// a device in this state, and only in this state.
    /// </remarks>
    Connected,
}