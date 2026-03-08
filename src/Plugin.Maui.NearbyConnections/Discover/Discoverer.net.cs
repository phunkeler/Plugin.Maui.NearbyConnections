namespace Plugin.Maui.NearbyConnections.Discover;

sealed partial class Discoverer : IDisposable
{
#pragma warning disable CA1822, S2325
    Task PlatformStartDiscovering()
#pragma warning restore CA1822, S2325
        => throw new PlatformNotSupportedException("This functionality is not supported in this platform implementation.");

#pragma warning disable CA1822, S2325
    void PlatformStopDiscovering()
#pragma warning restore CA1822, S2325
        => throw new PlatformNotSupportedException("This functionality is not supported in this platform implementation.");

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}