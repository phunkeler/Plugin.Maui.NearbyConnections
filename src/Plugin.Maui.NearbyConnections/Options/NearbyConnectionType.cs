namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies how aggressively a connection may use the radio, trading throughput against
/// disruption to other connections on the device.
/// </summary>
/// <remarks>
/// <b>Android only.</b> Multipeer Connectivity on iOS exposes no equivalent setting, so this value
/// is ignored there.
/// </remarks>
public enum NearbyConnectionType
{
    /// <summary>
    /// Balances throughput against disruption to other connections. This is the default and the
    /// recommended choice for mixed workloads, such as messaging with occasional attachments.
    /// </summary>
    Balanced,

    /// <summary>
    /// Prioritizes throughput at the cost of disrupting other connections on the device. Use this
    /// for large file transfers, where speed matters more than coexistence.
    /// </summary>
    HighBandwidth,

    /// <summary>
    /// Prioritizes leaving other connections undisturbed at the cost of throughput. Use this for
    /// small, infrequent messages alongside other network activity.
    /// </summary>
    NonDisruptive,
}