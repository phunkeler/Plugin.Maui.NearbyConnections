namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents the connection lifecycle state of a <see cref="NearbyDevice"/>.
/// A device exists in exactly one state at any given time.
/// </summary>
public enum NearbyDeviceState
{
    /// <summary>
    /// The device has been discovered but no connection has been initiated.
    /// </summary>
    Discovered,

    /// <summary>
    /// A remote device has requested a connection to this device (inbound).
    /// </summary>
    ConnectionRequestedInbound,

    /// <summary>
    /// This device has requested a connection to the remote device (outbound).
    /// </summary>
    ConnectionRequestedOutbound,

    /// <summary>
    /// A connection has been established with the device.
    /// </summary>
    Connected
}
