namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets how devices may connect to one another.
    /// </summary>
    /// <value>
    /// One of the <see cref="NearbyTopology"/> values. The default is
    /// <see cref="NearbyTopology.Cluster"/>.
    /// </value>
    /// <remarks>
    /// This value must match on the advertising and discovering devices, or they do not find each
    /// other. <b>This setting applies to Android only</b> and is ignored on iOS.
    /// </remarks>
    public NearbyTopology Topology { get; set; } = NearbyTopology.Cluster;

    /// <summary>
    /// Gets or sets a value indicating whether only low-power radios are used for advertising and
    /// discovery.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to restrict advertising and discovery to low-power radios such as
    /// Bluetooth Low Energy; otherwise, <see langword="false"/>. The default is
    /// <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// Enabling this option reduces battery consumption at the cost of range and throughput.
    /// <b>This setting applies to Android only</b> and is ignored on iOS.
    /// </remarks>
    public bool UseLowPower { get; set; }

    /// <summary>
    /// Gets or sets how aggressively a connection may use the radio.
    /// </summary>
    /// <value>
    /// One of the <see cref="NearbyConnectionType"/> values. The default is
    /// <see cref="NearbyConnectionType.Balanced"/>.
    /// </value>
    /// <remarks>
    /// <b>This setting applies to Android only</b> and is ignored on iOS.
    /// </remarks>
    public NearbyConnectionType ConnectionType { get; set; } = NearbyConnectionType.Balanced;

    /// <summary>
    /// Maps <see cref="Topology"/> onto the Google Nearby Connections strategy it names.
    /// </summary>
    /// <remarks>
    /// The mapping is the whole point of the neutral enum: it keeps
    /// <c>Android.Gms.Nearby.Connection.Strategy</c> out of the public surface, so consumers never
    /// have to reference a vendor SDK type to configure the plugin.
    /// </remarks>
    internal Strategy ToPlatformStrategy()
        => Topology switch
        {
            NearbyTopology.Star => Strategy.P2pStar,
            NearbyTopology.PointToPoint => Strategy.P2pPointToPoint,
            _ => Strategy.P2pCluster,
        };

    /// <summary>
    /// Maps <see cref="ConnectionType"/> onto the Google Nearby Connections constant it names.
    /// </summary>
    internal int ToPlatformConnectionType()
        => ConnectionType switch
        {
            NearbyConnectionType.HighBandwidth => Android.Gms.Nearby.Connection.ConnectionType.Disruptive,
            NearbyConnectionType.NonDisruptive => Android.Gms.Nearby.Connection.ConnectionType.NonDisruptive,
            _ => Android.Gms.Nearby.Connection.ConnectionType.Balanced,
        };

    private static partial string GetDefaultDisplayName() => DeviceInfo.Name;
    private static partial string GetDefaultServiceId() => AppInfo.Name;
    private static partial string GetDefaultReceivedFilesDirectory() => FileSystem.CacheDirectory;
}
