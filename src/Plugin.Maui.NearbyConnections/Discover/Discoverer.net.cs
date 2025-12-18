namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer : IDisposable
{
    Task PlatformStartDiscovering()
        => throw new NotImplementedException("Platform-specific discovering start logic must be implemented.");

    void PlatformStopDiscovering()
        => throw new NotImplementedException("Platform-specific discovering stop logic must be implemented.");

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
