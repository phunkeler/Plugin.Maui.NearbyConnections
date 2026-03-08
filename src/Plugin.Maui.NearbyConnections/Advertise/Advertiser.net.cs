namespace Plugin.Maui.NearbyConnections.Advertise;

sealed partial class Advertiser : IDisposable
{
#pragma warning disable CA1822, S2325
    Task PlatformStartAdvertising()
#pragma warning restore CA1822, S2325
        => throw new PlatformNotSupportedException("This functionality is not supported in this platform implementation.");

#pragma warning disable CA1822, S2325
    void PlatformStopAdvertising()
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