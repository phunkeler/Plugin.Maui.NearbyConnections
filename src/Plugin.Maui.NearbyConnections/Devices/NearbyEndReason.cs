namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies why a device left the device set, why a handshake ended, or why an established
/// connection ended. Carried by <see cref="NearbyDeviceChange.Reason"/> and by
/// <see cref="NearbyConnection.Disconnected"/>.
/// </summary>
public enum NearbyEndReason
{
    /// <summary>
    /// The local device rejected an inbound connection request, through
    /// <see cref="NearbyConnectionRequest.RejectAsync(CancellationToken)"/>.
    /// </summary>
    RequestRejected,

    /// <summary>
    /// The caller withdrew before the handshake completed, by cancelling the
    /// <see cref="CancellationToken"/> it passed.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The handshake did not complete within <see cref="NearbyOptions.ConnectTimeout"/> when this
    /// device initiated it, or before the offer's remaining window when it accepted.
    /// </summary>
    /// <remarks>
    /// This is distinct from <see cref="Failed"/> because the platforms differ: iOS has a native
    /// invitation timeout and Android has none, so the plugin supplies one on both. Collapsing the
    /// two would hide that asymmetry.
    /// </remarks>
    TimedOut,

    /// <summary>
    /// An inbound request was not answered before its offer deadline, so the library rejected it.
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

    /// <summary>
    /// An established connection ended without a local cause: the remote device closed it, or the
    /// link was lost. The platforms cannot tell those two apart, so neither does this library.
    /// </summary>
    Disconnected,

    /// <summary>
    /// The local application ended the connection, through
    /// <see cref="INearby.DisconnectAsync(NearbyDevice, CancellationToken)"/> or by disposing the
    /// <see cref="NearbyConnection"/>.
    /// </summary>
    DisconnectedByLocal,

    /// <summary>
    /// The session stopped — <see cref="INearby.StopAsync(CancellationToken)"/> or disposal —
    /// and ended the connection with it.
    /// </summary>
    SessionStopped,

    /// <summary>
    /// A discovery pass no longer reports the device, so it left the device set.
    /// </summary>
    LostFromDiscovery,
}
