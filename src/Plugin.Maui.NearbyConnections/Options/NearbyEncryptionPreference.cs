namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Whether the link between two devices must be encrypted.
/// </summary>
/// <remarks>
/// <strong>iOS only.</strong> Android's Nearby Connections encrypts every connection
/// unconditionally and exposes no equivalent setting, so this is ignored there — Android always
/// behaves as <see cref="Required"/>.
/// </remarks>
public enum NearbyEncryptionPreference
{
    /// <summary>
    /// Encryption is required; an unencrypted connection is refused. The default, and the right
    /// choice unless you have a specific reason otherwise.
    /// </summary>
    Required,

    /// <summary>
    /// Encryption is preferred but not required: the connection falls back to unencrypted if the
    /// peer does not support it.
    /// </summary>
    Optional,

    /// <summary>
    /// No encryption. Only appropriate for non-sensitive data on a trusted network — anything on
    /// the link can be read by other devices in range.
    /// </summary>
    None,
}
