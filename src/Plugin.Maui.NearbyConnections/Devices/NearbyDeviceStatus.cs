namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies the position of a <see cref="NearbyDevice"/> in its lifecycle, from first discovery
/// through to an established connection.
/// </summary>
/// <remarks>
/// A device remains in <see cref="INearbyConnections.Devices"/> in every one of these states. The
/// status changes rather than the device moving between collections.
/// </remarks>
public enum NearbyDeviceStatus
{
    /// <summary>
    /// The device has been discovered and is in range, with no connection negotiation in progress.
    /// This is the initial state for a discovered device, and the state a device returns to after a
    /// connection ends or a request is rejected.
    /// </summary>
    Visible,

    /// <summary>
    /// The device has requested a connection and is awaiting a response. Accept or reject the
    /// request to leave this state.
    /// </summary>
    RequestReceived,

    /// <summary>
    /// A connection handshake is in progress, in either direction. Check
    /// <see cref="NearbyDevice.Role"/> to determine which side initiated it.
    /// </summary>
    /// <remarks>
    /// This state is advisory and is not a guaranteed step in the lifecycle. On iOS, a device can
    /// move from an invitation directly to a disconnected state without ever being observed in
    /// this state, both when an invitation is declined and, less commonly, on error. Do not treat
    /// this state as a precondition for clearing pending connection state.
    /// </remarks>
    Connecting,

    /// <summary>
    /// A connection is established. <see cref="NearbyDevice.Connection"/> is not
    /// <see langword="null"/> in this state, and only in this state.
    /// </summary>
    Connected,
}
