namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets the amount of time to wait for the neary advertiser
    /// to respond to the invitation. The default value is 30 seconds.
    /// </summary>
    public TimeSpan InvitationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}