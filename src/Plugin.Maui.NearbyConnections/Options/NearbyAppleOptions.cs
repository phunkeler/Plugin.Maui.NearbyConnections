namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides Apple-specific configuration, exposed on every target framework.
/// </summary>
/// <remarks>
/// <para>
/// The settings on this type map onto Multipeer Connectivity knobs for which Google Nearby
/// Connections has no counterpart. They are exposed on every target framework so shared code
/// compiles without <c>#if IOS</c>. Running on Android, nothing reads them and they have no
/// effect.
/// </para>
/// <para>
/// Nesting these settings under <c>options.Apple</c> is deliberate disclosure: an expression such
/// as <c>options.Apple.EncryptionPreference</c> names the platform it applies to at the call site,
/// rather than leaving that fact to be discovered only by reading this comment.
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
    /// <b>Apple platforms only.</b> Android encrypts every connection unconditionally, so it always
    /// behaves as <see cref="NearbyEncryptionPreference.Required"/> regardless of this setting —
    /// lowering it here does not weaken an Android link.
    /// </remarks>
    public NearbyEncryptionPreference EncryptionPreference { get; set; } = NearbyEncryptionPreference.Required;

    /// <summary>
    /// Gets or sets how long a fresh call to <c>StartAdvertisingAsync</c> or
    /// <c>StartDiscoveryAsync</c> waits for Multipeer Connectivity's failure delegate before the
    /// call assumes the platform started successfully.
    /// </summary>
    /// <value>A <see cref="TimeSpan"/>. The default is 250 milliseconds.</value>
    /// <remarks>
    /// <b>Apple platforms only</b> — Android has no equivalent, because its platform start call is
    /// directly awaitable and this value has no effect there. Multipeer Connectivity has no
    /// start-success callback, only a delegate method that fires on failure. This window is the
    /// only way to translate that into a success/failure answer for the <c>Task</c> that
    /// <c>StartAdvertisingAsync</c>/<c>StartDiscoveryAsync</c> returns. A slow or
    /// thermally-throttled device can exceed the default window before the platform reports a
    /// genuine failure. That failure is not lost, but it no longer faults the already-returned
    /// <c>Task</c> — it downgrades to the same logged, post-start failure path as a radio dropping
    /// later. Raise this value if that downgrade is observed in the field.
    /// </remarks>
    public TimeSpan StartFailureGraceWindow { get; set; } = TimeSpan.FromMilliseconds(250);
}