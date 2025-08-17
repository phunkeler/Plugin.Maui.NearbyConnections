namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Partial class for starting/stopping discovery of nearby devices.
/// </summary>
public partial class Discoverer : IDisposable
{
    private bool _disposedValue;

    /// <summary>
    /// Starts discovering nearby devices.
    /// </summary>
    /// <param name="options">
    /// Options that modify discovery behavior.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    public Task PlatformStartDiscovering(DiscoveringOptions options, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Platform-specific discovering start logic must be implemented.");

    /// <summary>
    /// Stops discovering nearby devices.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
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
