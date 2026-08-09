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
}
