namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One-time app configuration for Nearby Connections.
/// Configure once at startup via <see cref="NearbyConnections.Configure"/>.
/// </summary>
public class NearbyConnectionsOptions
{
    /// <summary>
    /// The configuration section name for these options.
    /// </summary>
    public const string SectionName = "NearbyConnections";

    /// <summary>
    /// Service identifier for iOS Bonjour and Android service ID.
    /// Must match Info.plist NSBonjourServices configuration on iOS.
    /// </summary>
    public string ServiceName { get; set; } = AppInfo.Current.Name;

    /// <summary>
    /// When a nearby device requests a connection, automatically accept it.
    /// </summary>
    public bool AutoAcceptConnections { get; init; } = true;

#if ANDROID

    /// <summary>
    /// The <see cref="Android.App.Activity"/> (required for Google Nearby Connections API).
    /// </summary>
    public Android.App.Activity? Activity { get; set; } = Platform.CurrentActivity;

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
    /// Default is <see cref="Android.Gms.Nearby.Connection.ConnectionType.Balanced"/>.
    /// </summary>
    public int ConnectionType { get; set; } = Android.Gms.Nearby.Connection.ConnectionType.Balanced;
#endif
}