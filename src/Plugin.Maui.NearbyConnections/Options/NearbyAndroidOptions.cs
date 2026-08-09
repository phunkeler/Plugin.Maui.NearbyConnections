namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Android-specific configuration, exposed on every target framework.
/// </summary>
/// <remarks>
/// <para>
/// These settings map onto Google Nearby Connections knobs that Multipeer Connectivity has no
/// equivalent for. They exist on all platforms so shared code compiles without
/// <c>#if ANDROID</c>; on iOS they are read by nothing and have no effect.
/// </para>
/// <para>
/// The nesting is the disclosure: <c>options.Android.Topology</c> names the platform at the call
/// site, so a setting that does nothing on the current platform is visible in the expression
/// rather than only in this comment.
/// </para>
/// </remarks>
public sealed class NearbyAndroidOptions
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
    /// other. <b>Android only</b> — Multipeer Connectivity always behaves as a mesh.
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
    /// <b>Android only.</b>
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
    /// <b>Android only.</b> This is a distinct setting from <see cref="Topology"/>: topology decides
    /// who may connect to whom, this decides how hard the radio works once connected.
    /// </remarks>
    public NearbyConnectionType ConnectionType { get; set; } = NearbyConnectionType.Balanced;
}
