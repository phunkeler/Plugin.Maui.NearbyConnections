namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Where a <see cref="NearbyDevice"/> sits in its lifecycle, from first discovery through to an
/// established connection.
/// </summary>
/// <remarks>
/// A device stays in the session's device collection across every one of these states; the status
/// changes rather than the device moving between collections. See <c>docs/DEVICE-LIFECYCLE.md</c>.
/// </remarks>
public enum NearbyDeviceStatus
{
    /// <summary>
    /// Discovered and in range, with no negotiation in flight. The starting state for a device
    /// found while discovering, and the state a device returns to after a connection ends or a
    /// request is rejected.
    /// </summary>
    Visible,

    /// <summary>
    /// The remote device has asked to connect and is awaiting a response — accept or reject the
    /// pending request to leave this state.
    /// </summary>
    RequestReceived,

    /// <summary>
    /// A handshake is in flight, in either direction — outbound after a connect call, inbound after
    /// accepting a request. Check <see cref="NearbyDevice.Role"/> for the direction.
    /// </summary>
    /// <remarks>
    /// <strong>Advisory — not a guaranteed waypoint.</strong> On iOS a peer can go directly from an
    /// invitation to disconnected without ever being observed in this state, both on the common
    /// declined-invitation path and (rarely) on error. Never treat reaching <see cref="Connecting"/>
    /// as a precondition for clearing pending state, or the connection attempt can hang forever.
    /// </remarks>
    Connecting,

    /// <summary>
    /// A connection is established. <see cref="NearbyDevice.Connection"/> is non-<see langword="null"/>
    /// in this state and only in this state.
    /// </summary>
    Connected,
}
