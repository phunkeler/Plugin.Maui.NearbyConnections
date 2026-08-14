namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyOptions
{
    /// <summary>
    /// Maps <see cref="NearbyAppleOptions.EncryptionPreference"/> onto the MultipeerConnectivity
    /// value it names.
    /// </summary>
    /// <remarks>
    /// The mapping is the whole point of the neutral enum: it keeps
    /// <c>MCEncryptionPreference</c> out of the public surface, so consumers never have to
    /// reference a vendor SDK type to configure the plugin.
    /// </remarks>
    internal MCEncryptionPreference ToPlatformEncryptionPreference()
        => Apple.EncryptionPreference switch
        {
            NearbyEncryptionPreference.Optional => MCEncryptionPreference.Optional,
            NearbyEncryptionPreference.None => MCEncryptionPreference.None,
            _ => MCEncryptionPreference.Required,
        };

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
    private static partial string GetDefaultServiceId() => ServiceIdRules.Unset;

    private static partial string GetDefaultReceivedFilesDirectory() => FileSystem.AppDataDirectory;
}