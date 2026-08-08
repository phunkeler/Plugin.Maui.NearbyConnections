namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies which side of a connection the local device represents.
/// </summary>
public enum ConnectionRole
{
    /// <summary>
    /// The local device initiated the connection by calling
    /// <see cref="INearbyConnections.ConnectAsync(NearbyDevice, CancellationToken)"/>.
    /// </summary>
    Initiator,

    /// <summary>
    /// The local device accepted an inbound connection request by calling
    /// <see cref="INearbyConnections.AcceptAsync(NearbyDevice, CancellationToken)"/>.
    /// </summary>
    Acceptor,
}
