namespace Plugin.Maui.NearbyConnections.Advertise;
/// <summary>
/// Makes the current device discoverable for nearby peer-to-peer connections.
/// </summary>
public interface IAdvertiser : IDisposable
{
    /// <summary>
    /// REMOVE THIS. Consumers will react through a mediator
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// The general interface for advertising state changes.
    /// </summary>
    /// <remarks>
    /// This event is fired whenever the advertising state changes, providing the previous
    /// and current state.
    /// </remarks>
    event EventHandler<AdvertisingStateChangedEventArgs> StateChanged;

    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options">
    /// The <see cref="AdvertisingOptions"/> to configure advertising behavior.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to cancel the operation.
    /// </param>
    /// <returns> A task representing the asynchronous operation.</returns>
    Task<IAdvertisingSession> StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising for this session.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);
}