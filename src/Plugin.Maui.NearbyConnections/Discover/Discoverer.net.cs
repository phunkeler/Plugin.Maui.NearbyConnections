namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Partial class for starting/stopping discovery of nearby devices.
/// </summary>
internal sealed partial class Discoverer
{
    Task PlatformStartDiscovering()
        => throw new NotImplementedException("Platform-specific discovering start logic must be implemented.");

    public void PlatformStopDiscovering()
        => throw new NotImplementedException("Platform-specific discovering stop logic must be implemented.");
}
