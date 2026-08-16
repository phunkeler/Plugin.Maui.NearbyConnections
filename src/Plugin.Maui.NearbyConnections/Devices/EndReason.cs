namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies why a handshake ended before a connection was established.
/// </summary>
/// <remarks>
/// <para>
/// <b>Internal: this reaches logs only.</b> A handshake that fails surfaces its reason to the caller
/// as the exception <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/> or
/// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> throws; a drop after a
/// connection was established surfaces as the device returning to
/// <see cref="NearbyDeviceStatus.Visible"/>, which does not carry a reason.
/// </para>
/// <para>
/// It was public through 0.3.0-preview and no consumer could observe a value, because nothing
/// returns one. If a future design attaches a reason to the transition — a nullable reason on
/// <see cref="NearbyDeviceChange"/>, say — this becomes public again, with only the cases that
/// design actually produces.
/// </para>
/// </remarks>
enum EndReason
{
    /// <summary>
    /// The local device rejected an inbound connection request, through
    /// <see cref="INearby.RejectAsync(NearbyDevice, CancellationToken)"/>.
    /// </summary>
    LocalRejected,

    /// <summary>
    /// The caller withdrew before the handshake completed, by cancelling the
    /// <see cref="CancellationToken"/> passed to
    /// <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/> or
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/>.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The handshake did not complete within <see cref="NearbyOptions.ConnectTimeout"/> when this
    /// device initiated it, or <see cref="NearbyOptions.AcceptTimeout"/> when it accepted.
    /// </summary>
    /// <remarks>
    /// This is distinct from <see cref="Failed"/> because the platforms differ: iOS has a native
    /// invitation timeout and Android has none, so the plugin supplies one on both. Collapsing the
    /// two would hide that asymmetry.
    /// </remarks>
    TimedOut,

    /// <summary>
    /// An inbound request was not answered within
    /// <see cref="NearbyOptions.InboundRequestTimeout"/>, so the library rejected it.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="TimedOut"/>, which is an operation the caller was awaiting. Nothing
    /// awaits an outstanding request, so this reason reports a withdrawn offer rather than a failed
    /// call.
    /// </remarks>
    RequestExpired,

    /// <summary>
    /// The handshake failed for any other reason.
    /// </summary>
    Failed,
}