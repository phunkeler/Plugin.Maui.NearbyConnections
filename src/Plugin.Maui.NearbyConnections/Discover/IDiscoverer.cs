namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// / discovery lifecycle state and operations.
/// </summary>
public interface IDiscoverer : IDisposable
{
    /// <summary>
    /// Fired when discovering state changes.
    /// </summary>
    event EventHandler<DiscoveringStateChangedEventArgs> DiscoveringStateChanged;

    /// <summary>
    /// Gets a value indicating whether discovering is currently active.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Starts discovering with the specified options.
    /// </summary>
    /// <param name="options">The discovering options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartDiscoveringAsync(DiscoveringOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovering.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopDiscoveringAsync(CancellationToken cancellationToken = default);
}
