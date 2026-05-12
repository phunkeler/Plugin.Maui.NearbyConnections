namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets the encryption preference for the underlying <see cref="MCSession"/>.
    /// The default value is <see cref="MCEncryptionPreference.Required"/>.
    /// </summary>
    public MCEncryptionPreference EncryptionPreference { get; set; } = MCEncryptionPreference.Required;

    /// <summary>
    /// Gets or sets the amount of time to wait for the nearby advertiser
    /// to respond to the invitation. The default value is 30 seconds.
    /// </summary>
    public TimeSpan InvitationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    private static partial string GetDefaultDisplayName() => DeviceInfo.Name;

    /// <remarks>
    /// On iOS there is no meaningful default for <c>ServiceId</c>: it must be a valid Bonjour
    /// service type in the form <c>_&lt;name&gt;._tcp</c> or <c>_&lt;name&gt;._udp</c> and must
    /// match an entry in the app's <c>Info.plist</c> under <c>NSBonjourServices</c>.
    /// The sentinel value <c>"_UNSET._tcp"</c> is intentionally invalid so that validation
    /// at startup throws immediately with a descriptive message rather than silently failing
    /// deep inside MultipeerConnectivity.
    /// </remarks>
#pragma warning disable S3400 // Partial method implementation — cannot be replaced with a constant
    private static partial string GetDefaultServiceId() => "_UNSET._tcp";
#pragma warning restore S3400

    private static partial string GetDefaultReceivedFilesDirectory() => FileSystem.AppDataDirectory;
}