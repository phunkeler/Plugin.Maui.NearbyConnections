namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Makes the current device discoverable for nearby peer-to-peer connections.
/// </summary>
public interface IAdvertiser : IDisposable
{
    /// <summary>
    /// Fired when the advertiser's state changes.
    /// </summary>
    event EventHandler<AdvertisingStateChangedEventArgs> AdvertisingStateChanged;

    /// <summary>
    /// Gets a value indicating whether this object is currently advertising.
    /// Advertising can be started with <see cref="StartAdvertisingAsync"/>
    /// and stopped by <see cref="StopAdvertisingAsync"/>.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options">
    /// The <see cref="AdvertisingOptions"/> .
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to cancel the operation.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);
}