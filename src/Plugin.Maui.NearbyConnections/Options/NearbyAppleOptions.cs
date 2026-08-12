namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Apple-specific configuration, exposed on every target framework.
/// </summary>
/// <remarks>
/// <para>
/// These settings map onto Multipeer Connectivity knobs that Google Nearby Connections has no
/// equivalent for. They exist on all platforms so shared code compiles without <c>#if IOS</c>;
/// on Android they are read by nothing and have no effect.
/// </para>
/// <para>
/// The nesting is the disclosure: <c>options.Apple.EncryptionPreference</c> names the platform at
/// the call site, so a setting that does nothing on the current platform is visible in the
/// expression rather than only in this comment.
/// </para>
/// </remarks>
public sealed class NearbyAppleOptions
{
    /// <summary>
    /// Gets or sets whether the link between two devices must be encrypted.
    /// </summary>
    /// <value>
    /// One of the <see cref="NearbyEncryptionPreference"/> values. The default is
    /// <see cref="NearbyEncryptionPreference.Required"/>.
    /// </value>
    /// <remarks>
    /// <b>Apple platforms only.</b> Android encrypts every connection unconditionally and always
    /// behaves as <see cref="NearbyEncryptionPreference.Required"/>, so lowering this does not
    /// weaken an Android link.
    /// </remarks>
    public NearbyEncryptionPreference EncryptionPreference { get; set; } = NearbyEncryptionPreference.Required;

    /// <summary>
    /// Gets or sets how long a fresh call to <c>StartAdvertisingAsync</c> or
    /// <c>StartDiscoveryAsync</c> waits for MultipeerConnectivity's failure delegate before assuming
    /// the platform started successfully.
    /// </summary>
    /// <value>A <see cref="TimeSpan"/>. The default is 250 milliseconds.</value>
    /// <remarks>
    /// <b>Apple platforms only.</b> Multipeer Connectivity has no start-success callback, only a
    /// delegate method that fires on failure — this window is the only way to translate that into
    /// "did starting succeed" for the Task <c>StartAdvertisingAsync</c>/<c>StartDiscoveryAsync</c>
    /// returns. A slow or thermally-throttled device can exceed the default window before the
    /// platform reports a genuine failure; that failure is not lost, but it downgrades to the
    /// same logged, post-start failure path as a radio dropping later — it no longer faults the
    /// already-returned Task. Raise this value if that downgrade is observed in the field. Android
    /// has no equivalent because its platform start call is directly awaitable and this value has
    /// no effect there.
    /// </remarks>
    public TimeSpan StartFailureGraceWindow { get; set; } = TimeSpan.FromMilliseconds(250);
}
