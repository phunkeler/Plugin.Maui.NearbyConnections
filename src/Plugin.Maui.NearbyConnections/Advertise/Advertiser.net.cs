namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
public partial class Advertiser : IDisposable
{
    private bool _disposedValue;

    /// <summary>
    /// Starts advertising for nearby connections.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task PlatformStartAdvertising(AdvertisingOptions options, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Platform-specific advertising start logic must be implemented.");

    /// <summary>
    /// Stops advertising for nearby connections.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task PlatformStopAdvertising(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Platform-specific advertising stop logic must be implemented.");

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}