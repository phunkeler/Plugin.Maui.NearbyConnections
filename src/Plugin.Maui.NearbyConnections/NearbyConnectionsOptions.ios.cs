namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets the encryption preference for the underlying <see cref="MCSession"/>.
    /// The default value is <see cref="MCEncryptionPreference.Required"/>.
    /// </summary>
    public MCEncryptionPreference EncryptionPreference { get; init; } = MCEncryptionPreference.Required;

    /// <summary>
    /// Gets or sets the amount of time to wait for the neary advertiser
    /// to respond to the invitation. The default value is 30 seconds.
    /// </summary>
    public TimeSpan InvitationTimeout { get; init; } = TimeSpan.FromSeconds(30);
}