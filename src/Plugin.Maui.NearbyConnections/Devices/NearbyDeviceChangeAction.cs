namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies what happened to a device in a <see cref="NearbyDeviceChange"/>.
/// </summary>
public enum NearbyDeviceChangeAction
{
    /// <summary>
    /// The device became known to the session and was not present before.
    /// </summary>
    Added,

    /// <summary>
    /// A device already known to the session changed — its
    /// <see cref="NearbyDevice.Status"/>, <see cref="NearbyDevice.Role"/>, or
    /// <see cref="NearbyDevice.DisplayName"/> is not what it was.
    /// </summary>
    /// <remarks>
    /// Every connection lifecycle transition arrives as one of these. A device that receives a
    /// connection request, connects, or drops is updated, not removed and re-added.
    /// </remarks>
    Updated,

    /// <summary>
    /// The device is no longer known to the session.
    /// </summary>
    Removed,
}
