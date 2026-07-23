namespace Plugin.Maui.NearbyDevices;

public sealed partial class NearbyDevicesOptions
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
    /// On iOS there is no meaningful default for <c>ServiceId</c>: it is passed directly as
    /// <c>MCNearbyServiceAdvertiser</c>/<c>MCNearbyServiceBrowser</c>'s <c>serviceType</c>,
    /// which Apple requires to be a bare string 1-15 characters long identifying the network
    /// protocol (e.g. <c>"xamarin-txtchat"</c>) — it is NOT a DNS-SD/Bonjour service type in
    /// the <c>_name._tcp</c> form used by <c>Info.plist</c>'s <c>NSBonjourServices</c> entries.
    /// Passing a string in that form (or over 15 characters) causes
    /// <c>MCNearbyServiceAdvertiser</c>'s native initializer to throw an unmanaged
    /// <c>NSInvalidArgumentException</c> that crosses the native/managed boundary as a fatal
    /// native crash rather than a catchable .NET exception.
    /// The sentinel value <c>"_UNSET"</c> is intentionally invalid (over the eventual length
    /// budget is not needed here since any un-overridden value should fail loudly) so that
    /// validation at startup throws immediately with a descriptive message instead.
    /// </remarks>
    private static partial string GetDefaultServiceId() => "_UNSET";

    private static partial string GetDefaultReceivedFilesDirectory() => FileSystem.AppDataDirectory;
}