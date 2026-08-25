namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A single change to the set of devices known to an <see cref="INearby"/>, delivered through
/// <see cref="INearbyDevices.Changes"/>.
/// </summary>
/// <param name="Action">The kind of change this is.</param>
/// <param name="Device">
/// The device as it is after the change. This instance never updates itself. A later change to the
/// same device is delivered as a new <see cref="NearbyDeviceChange"/> carrying a new instance.
/// </param>
/// <remarks>
/// This is a delta, not a whole-list snapshot — a consumer applies the one change described here
/// instead of re-diffing the entire collection on every transition.
/// </remarks>
public sealed record NearbyDeviceChange(NearbyDeviceChangeAction Action, NearbyDevice Device)
{
    /// <summary>
    /// Gets why the device left or a connection ended, when the library observed a reason locally.
    /// </summary>
    /// <value>
    /// The locally-observed reason, or <see langword="null"/> for a change with no ending in it —
    /// a device found, a request received, a handshake that succeeded.
    /// </value>
    public NearbyEndReason? Reason { get; init; }
}