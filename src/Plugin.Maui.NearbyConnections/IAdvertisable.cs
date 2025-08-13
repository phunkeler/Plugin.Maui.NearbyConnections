namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Manages advertising lifecycle state and operations.
/// </summary>
public interface IAdvertisable : IDisposable
{
    /// <summary>
    /// Fired when advertising state changes.
    /// </summary>
    event EventHandler<AdvertisingStateChangedEventArgs> AdvertisingStateChanged;

    /// <summary>
    /// Gets a value indicating whether advertising is currently active.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options">The advertising options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAdvertisingAsync(IAdvertisingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);
}