namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Provides operations for discovering nearby advertising devices.
/// </summary>
public interface IDiscoverer : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the device is actively discovering nearby advertisers.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Start discovering nearby advertising devices.
    /// </summary>
    /// <param name="options">The options controlling discovery.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartDiscoveringAsync(DiscoverOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovering.
    /// </summary>
    void StopDiscovering();
}
