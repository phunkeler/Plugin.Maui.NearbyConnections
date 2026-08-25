namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// An inbound connection request from a nearby device, delivered through
/// <see cref="INearby.Requests"/>. Answer it with <see cref="AcceptAsync(CancellationToken)"/> or
/// <see cref="RejectAsync(CancellationToken)"/> before
/// <see cref="NearbyOptions.InboundRequestTimeout"/> elapses.
/// </summary>
/// <remarks>
/// <para>
/// The request is a small mirror of <see cref="NearbyConnection"/>: one operation pair, one
/// lifetime signal (<see cref="Expired"/>), one typed failure
/// (<see cref="NearbyRequestExpiredException"/>). The accept and reject decision lives here, on
/// the request, because this object is where the decision is made — a request can be answered
/// once, and only while it is outstanding.
/// </para>
/// <para>
/// With <see cref="NearbyOptions.AutoAcceptConnectionRequests"/> enabled the session answers on
/// the application's behalf, <see cref="INearby.Requests"/> never yields, and the connection
/// arrives through <see cref="INearby.Connections"/> instead.
/// </para>
/// </remarks>
public sealed class NearbyConnectionRequest
{
    readonly TaskCompletionSource _expired = new(TaskCreationOptions.RunContinuationsAsynchronously);

    Func<CancellationToken, Task<NearbyConnection>> _acceptGateway;
    Func<CancellationToken, Task> _rejectGateway;

    internal NearbyConnectionRequest(
        NearbyDevice remoteDevice,
        Func<CancellationToken, Task<NearbyConnection>> accept,
        Func<CancellationToken, Task> reject)
    {
        RemoteDevice = remoteDevice;
        AcceptCore = accept;
        RejectCore = reject;
        _acceptGateway = accept;
        _rejectGateway = reject;
    }

    /// <summary>
    /// Gets the device asking to connect.
    /// </summary>
    /// <value>The remote device. Its display name is remote-chosen, sanitized input.</value>
    public NearbyDevice RemoteDevice { get; }

    /// <summary>
    /// Gets a task that completes when this request stops being answerable — it expired, or the
    /// session stopped.
    /// </summary>
    /// <value>
    /// A task that completes when the request is withdrawn. It never faults and never completes
    /// for a request that was answered in time. A view awaits it to dismiss its prompt.
    /// </value>
    public Task Expired => _expired.Task;

    /// <summary>
    /// Accepts the request and completes the handshake.
    /// </summary>
    /// <param name="cancellationToken">A token to stop waiting for the handshake.</param>
    /// <returns>The established connection — the same instance <see cref="INearby.Connections"/> yields.</returns>
    /// <exception cref="NearbyRequestExpiredException">
    /// The request is no longer outstanding: it expired, or it was already answered.
    /// </exception>
    /// <exception cref="NearbyConnectionTimeoutException">
    /// The handshake did not complete within <see cref="NearbyOptions.AcceptTimeout"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public Task<NearbyConnection> AcceptAsync(CancellationToken cancellationToken = default)
        => _acceptGateway(cancellationToken);

    /// <summary>
    /// Rejects the request. The remote device observes a failed handshake.
    /// </summary>
    /// <param name="cancellationToken">A token to stop waiting for the rejection to be sent.</param>
    /// <returns>A task that completes once the rejection is signaled to the platform.</returns>
    /// <exception cref="NearbyRequestExpiredException">
    /// The request is no longer outstanding: it expired, or it was already answered.
    /// </exception>
    public Task RejectAsync(CancellationToken cancellationToken = default)
        => _rejectGateway(cancellationToken);

    /// <summary>
    /// The platform half of accepting, without the session effects. The session's gateway calls
    /// this after it wins the claim; the platform layer and tests reach it directly.
    /// </summary>
    internal Func<CancellationToken, Task<NearbyConnection>> AcceptCore { get; }

    /// <summary>The platform half of rejecting. See <see cref="AcceptCore"/>.</summary>
    internal Func<CancellationToken, Task> RejectCore { get; }

    /// <summary>
    /// Routes the public answer operations through the session — the claim, the registry
    /// transitions, and the delivery publish. The session attaches these when its pump surfaces
    /// the request; an unattached request answers through the platform core alone.
    /// </summary>
    internal void AttachSession(
        Func<CancellationToken, Task<NearbyConnection>> acceptGateway,
        Func<CancellationToken, Task> rejectGateway)
    {
        _acceptGateway = acceptGateway;
        _rejectGateway = rejectGateway;
    }

    /// <summary>Completes <see cref="Expired"/>. Idempotent.</summary>
    internal void MarkExpired() => _expired.TrySetResult();
}
