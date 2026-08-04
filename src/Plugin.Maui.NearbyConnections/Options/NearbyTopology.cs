namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// How devices may connect to one another — how many peers each side can hold at once, and the
/// bandwidth that follows from that.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Must match on both sides.</strong> An advertiser and a discoverer configured with
/// different topologies will not find each other.
/// </para>
/// <para>
/// <strong>Android only.</strong> MultipeerConnectivity has no equivalent knob — iOS is always
/// effectively a mesh — so this is ignored on iOS. It is named for the shape it describes rather
/// than after either platform's vocabulary.
/// </para>
/// </remarks>
public enum NearbyTopology
{
    /// <summary>
    /// Many-to-many: every device may connect to several others at once. The most flexible option
    /// and the right default for group scenarios like chat, at lower per-connection bandwidth.
    /// </summary>
    Cluster,

    /// <summary>
    /// One-to-many, high bandwidth: one device accepts connections from several others, but those
    /// others connect only to it. Suits one device distributing data to a group.
    /// </summary>
    Star,

    /// <summary>
    /// One-to-one, highest bandwidth: each device connects to exactly one peer. Choose this when
    /// transferring large files between two devices and nothing else needs to join.
    /// </summary>
    PointToPoint,
}
