namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// How aggressively a connection may use the radio, trading throughput against disruption to other
/// connections on the device.
/// </summary>
/// <remarks>
/// <strong>Android only.</strong> MultipeerConnectivity exposes no equivalent, so this is ignored
/// on iOS.
/// </remarks>
public enum NearbyConnectionType
{
    /// <summary>
    /// Balances throughput against disruption to other connections. The default, and the right
    /// choice for mixed workloads like chat with occasional attachments.
    /// </summary>
    Balanced,

    /// <summary>
    /// Prioritises throughput, at the cost of disrupting other connections on the device. Suits
    /// large file transfers where speed matters more than coexistence.
    /// </summary>
    HighBandwidth,

    /// <summary>
    /// Prioritises not disrupting other connections, at the cost of throughput. Suits small,
    /// infrequent messages alongside other network activity.
    /// </summary>
    NonDisruptive,
}
