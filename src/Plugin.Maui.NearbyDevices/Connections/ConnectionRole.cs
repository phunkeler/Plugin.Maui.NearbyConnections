namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Indicates whether the local device initiated the connection (Initiator)
/// or accepted an inbound request (Acceptor).
/// </summary>
public enum ConnectionRole
{
    /// <summary>
    /// The local device initiated the connection by calling ConnectAsync.
    /// </summary>
    Initiator,

    /// <summary>
    /// The local device accepted an inbound connection request by calling AcceptAsync.
    /// </summary>
    Acceptor,
}
