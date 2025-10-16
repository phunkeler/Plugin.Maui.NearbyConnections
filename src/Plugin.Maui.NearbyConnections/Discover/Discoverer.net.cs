namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Partial class for starting/stopping discovery of nearby devices.
/// </summary>
public partial class Discoverer
{
    public Task PlatformStartDiscovering(DiscoverOptions options)
        => throw new NotImplementedException("Platform-specific discovering start logic must be implemented.");

    public void PlatformStopDiscovering()
        => throw new NotImplementedException("Platform-specific discovering stop logic must be implemented.");
}
