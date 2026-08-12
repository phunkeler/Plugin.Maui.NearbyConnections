namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies how devices may connect to one another: how many peers each device can hold at once,
/// and the bandwidth that results.
/// </summary>
/// <remarks>
/// <para>
/// <b>This value must match on both devices.</b> An advertising device and a discovering device
/// configured with different topologies do not find each other.
/// </para>
/// <para>
/// <b>Android only.</b> Multipeer Connectivity on iOS has no equivalent setting and always behaves
/// as a mesh, so this value is ignored there.
/// </para>
/// </remarks>
public enum NearbyTopology
{
    /// <summary>
    /// Many-to-many. Every device may connect to several others at once. This is the most flexible
    /// option and the recommended default for group scenarios, at the cost of lower
    /// per-connection bandwidth.
    /// </summary>
    Cluster,

    /// <summary>
    /// One-to-many, at high bandwidth. One device accepts connections from several others, and
    /// those devices connect only to it. Use this when one device distributes data to a group.
    /// </summary>
    Star,

    /// <summary>
    /// One-to-one, at the highest bandwidth. Each device connects to exactly one peer. Use this
    /// when transferring large files between two devices.
    /// </summary>
    PointToPoint,
}
