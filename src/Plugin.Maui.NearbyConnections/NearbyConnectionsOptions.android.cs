namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets the advertising/discovery strategy.
    /// Must match between advertising and discovery sessions.
    /// Default is <see cref="Strategy.P2pCluster"/>.
    /// </summary>
    public Strategy Strategy { get; set; } = Strategy.P2pCluster;

    /// <summary>
    /// Gets or sets whether low power mode should be used.
    /// If <see langword="true" />, only low power mediums (like BLE) will be used for advertising and discovery.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool UseLowPower { get; set; }

    /// <summary>
    /// Gets or sets the Android connection type.
    /// Default is <see cref="ConnectionType.Balanced"/>.
    /// </summary>
    public int ConnectionType { get; set; } = Android.Gms.Nearby.Connection.ConnectionType.Balanced;
}