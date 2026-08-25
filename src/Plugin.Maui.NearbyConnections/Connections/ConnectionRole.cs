namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies which side of a connection handshake the local device played.
/// </summary>
public enum ConnectionRole
{
    /// <summary>
    /// The local device initiated the connection, through
    /// <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/>.
    /// </summary>
    Initiator,

    /// <summary>
    /// The local device accepted an inbound request, through
    /// <see cref="NearbyConnectionRequest.AcceptAsync(CancellationToken)"/>.
    /// </summary>
    Acceptor,
}