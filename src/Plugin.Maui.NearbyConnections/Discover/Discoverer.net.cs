namespace Plugin.Maui.NearbyConnections;

public partial class NearbyConnectionsDiscoverer : IDisposable
{
    private bool _disposedValue;

    /// <summary>
    /// Starts discovering for nearby connections.
    /// </summary>
    /// <param name="options">
    /// Options that modify discovery behavior.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// ßƒA task representing the asynchronous operation.
    /// </returns>
    /// <exception cref="NotImplementedException">
    /// </exception>
    public Task PlatformStartDiscovering(IDiscoveringOptions options, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Platform-specific discovering start logic must be implemented.");

    /// <summary>
    /// Stops discovering for nearby connections.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task PlatformStopDiscovering(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Platform-specific discovering stop logic must be implemented.");

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
