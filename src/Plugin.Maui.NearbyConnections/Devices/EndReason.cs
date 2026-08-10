namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies why a connection to a <see cref="NearbyDevice"/> ended, or why a handshake ended
/// before one was established.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is currently reported in logs only.</b> A handshake that fails surfaces its reason to
/// the caller as the exception <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/>
/// or <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> throws; a drop after a
/// connection was established surfaces as the device returning to
/// <see cref="NearbyDeviceStatus.Visible"/>, which does not carry a reason. Attaching the reason to
/// that transition is not yet designed — see <c>docs/THREADING.md</c>.
/// </para>
/// The plugin never guesses. A reason other than <see cref="Unknown"/> is reported only when the
/// platform stated it, or when the plugin itself caused the ending. Platforms differ in how much
/// they attribute, so <see cref="Unknown"/> is a legitimate outcome rather than an error.
/// </remarks>
public enum EndReason
{
    /// <summary>
    /// The connection ended for a reason the platform did not report. This is the default, and on
    /// iOS it is the usual outcome for a handshake that fails without the local device having
    /// caused it: MultipeerConnectivity reports a state change without a cause.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// An established connection was lost, from either side. This covers both a deliberate
    /// disconnect and a peer going out of range.
    /// </summary>
    Disconnected,

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
    /// The handshake was not answered within
    /// <see cref="NearbyOptions.InvitationTimeout"/>.
    /// </summary>
    /// <remarks>
    /// This is distinct from <see cref="Failed"/> because the platforms differ: iOS has a native
    /// invitation timeout and Android has none, so the plugin supplies one on both. Collapsing the
    /// two would hide that asymmetry.
    /// </remarks>
    TimedOut,

    /// <summary>
    /// The handshake failed for any other reason.
    /// </summary>
    Failed,
}
