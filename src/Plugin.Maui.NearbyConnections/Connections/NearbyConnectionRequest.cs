namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents an inbound connection request from a remote device received while advertising.
/// The consumer either accepts (obtaining a <see cref="NearbyConnection"/>) or rejects the request.
/// </summary>
public sealed class NearbyConnectionRequest
{
    readonly Func<CancellationToken, Task<NearbyConnection>> _acceptFactory;
    readonly Func<CancellationToken, Task> _rejectFactory;

    /// <summary>
    /// Gets the remote device that sent the connection request.
    /// </summary>
    public NearbyDevice RemoteDevice { get; }

    /// <summary>
    /// Initializes a new <see cref="NearbyConnectionRequest"/> for use in test doubles of <see cref="INearbyConnections"/>.
    /// </summary>
    /// <param name="remoteDevice">The device sending the connection request.</param>
    /// <param name="acceptFactory">A delegate invoked when <see cref="AcceptAsync"/> is called.</param>
    /// <param name="rejectFactory">A delegate invoked when <see cref="RejectAsync"/> is called.</param>
    public NearbyConnectionRequest(
        NearbyDevice remoteDevice,
        Func<CancellationToken, Task<NearbyConnection>> acceptFactory,
        Func<CancellationToken, Task> rejectFactory)
    {
        RemoteDevice = remoteDevice;
        _acceptFactory = acceptFactory;
        _rejectFactory = rejectFactory;
    }

    /// <summary>
    /// Accepts the connection request and returns a <see cref="NearbyConnection"/> representing
    /// the established session with the remote device.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the accept operation.</param>
    /// <returns>
    /// A task that resolves to a <see cref="NearbyConnection"/> when the platform confirms
    /// the connection is established.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public Task<NearbyConnection> AcceptAsync(CancellationToken cancellationToken = default)
        => _acceptFactory(cancellationToken);

    /// <summary>
    /// Rejects the connection request. The remote device will be notified that the connection
    /// was declined.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reject operation.</param>
    /// <returns>A task that completes when the rejection has been signaled to the platform.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public Task RejectAsync(CancellationToken cancellationToken = default)
        => _rejectFactory(cancellationToken);
}
