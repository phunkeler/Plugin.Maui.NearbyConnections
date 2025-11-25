namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer
{
    Task PlatformStartDiscovering()
        => throw new NotImplementedException("Platform-specific discovering start logic must be implemented.");

    public void PlatformStopDiscovering()
        => throw new NotImplementedException("Platform-specific discovering stop logic must be implemented.");

    public void Dispose()
    {
        // No resources to dispose on non-mobile platforms
    }
}
