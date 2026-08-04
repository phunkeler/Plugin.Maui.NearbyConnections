namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets how devices may connect to one another.
    /// Default is <see cref="NearbyTopology.Cluster"/>.
    /// </summary>
    /// <remarks>
    /// Must match between the advertising and discovering devices, or they will not find each
    /// other. Android only — iOS has no equivalent and ignores this.
    /// </remarks>
    public NearbyTopology Topology { get; set; } = NearbyTopology.Cluster;

    /// <summary>
    /// Gets or sets whether low power mode should be used.
    /// If <see langword="true" />, only low power mediums (like BLE) will be used for advertising and discovery.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool UseLowPower { get; set; }

    /// <summary>
    /// Gets or sets how aggressively a connection may use the radio.
    /// Default is <see cref="NearbyConnectionType.Balanced"/>.
    /// </summary>
    /// <remarks>Android only — iOS has no equivalent and ignores this.</remarks>
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
