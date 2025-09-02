namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Discovery lifecycle state and operations.
/// </summary>
public interface IDiscoverer : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the device is actively discovering nearby advertisers.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Starts discovering with the specified options.
    /// </summary>
    /// <param name="options">The discovering options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartDiscoveringAsync(DiscoverOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovering.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopDiscoveringAsync(CancellationToken cancellationToken = default);
}
