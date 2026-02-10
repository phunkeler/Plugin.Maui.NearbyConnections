namespace Plugin.Maui.NearbyConnections.Advertise;

sealed partial class Advertiser : IDisposable
{
    Task PlatformStartAdvertising()
        => throw new NotImplementedException("Platform-specific advertising start logic must be implemented.");

    void PlatformStopAdvertising()
        => throw new NotImplementedException("Platform-specific advertising stop logic must be implemented.");

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // No platform resources to dispose on non-mobile platforms
            GC.SuppressFinalize(this);
        }
    }
}