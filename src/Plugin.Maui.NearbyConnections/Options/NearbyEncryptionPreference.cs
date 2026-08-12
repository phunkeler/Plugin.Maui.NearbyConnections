namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies whether the link between two devices must be encrypted.
/// </summary>
/// <remarks>
/// <b>iOS only.</b> Nearby Connections on Android encrypts every connection unconditionally and
/// exposes no equivalent setting, so this value is ignored there — Android always behaves as
/// <see cref="Required"/>.
/// </remarks>
public enum NearbyEncryptionPreference
{
    /// <summary>
    /// Encryption is required, and an unencrypted connection is refused. This is the default and
    /// the recommended choice.
    /// </summary>
    Required,

    /// <summary>
    /// Encryption is preferred but not required. The connection falls back to an unencrypted link
    /// if the remote device does not support encryption.
    /// </summary>
    Optional,

    /// <summary>
    /// Encryption is not used. Choose this only for data that is not sensitive: any device in range
    /// can read traffic on the link.
    /// </summary>
    None,
}
