namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets the anount of time (in seconds) to wait for the neary advertiser
    /// to respond to the invitation. The default value is 30 seconds.
    /// </summary>
    public double InvitationTimeout { get; set; } = 30.0;
}