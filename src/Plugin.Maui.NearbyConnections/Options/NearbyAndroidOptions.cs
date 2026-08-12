namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides Android-specific configuration, exposed on every target framework.
/// </summary>
/// <remarks>
/// <para>
/// The settings on this type map onto Google Nearby Connections knobs for which Multipeer
/// Connectivity has no counterpart. They are exposed on every target framework so shared code
/// compiles without <c>#if ANDROID</c>; running on iOS, nothing reads them and they have no
/// effect.
/// </para>
/// <para>
/// Nesting these settings under <c>options.Android</c> is deliberate disclosure: an expression
/// such as <c>options.Android.Topology</c> names the platform it applies to at the call site,
/// rather than leaving that fact to be discovered only by reading this comment.
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
    /// <b>Android only</b> — Multipeer Connectivity always behaves as a mesh and has no equivalent
    /// setting. The advertising device and the discovering device must agree on this value, or they
    /// do not find each other.
    /// </remarks>
    public NearbyTopology Topology { get; set; } = NearbyTopology.Cluster;

    /// <summary>
    /// Gets or sets a value that indicates whether advertising and discovery are restricted to
    /// low-power radios.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to restrict advertising and discovery to low-power radios such as
    /// Bluetooth Low Energy; otherwise, <see langword="false"/>. The default is
    /// <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// <b>Android only.</b> Enabling this option reduces battery consumption at the cost of range
    /// and throughput.
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
    /// <b>Android only.</b> This is a distinct knob from <see cref="Topology"/>: topology decides
    /// who may connect to whom, and this decides how hard the radio works once a connection exists.
    /// </remarks>
    public NearbyConnectionType ConnectionType { get; set; } = NearbyConnectionType.Balanced;
}
