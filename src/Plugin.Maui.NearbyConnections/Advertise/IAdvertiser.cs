namespace Plugin.Maui.NearbyConnections.Advertise;
/// <summary>
/// Manages advertising for this device.
/// </summary>
public interface IAdvertiser : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether this device is currently advertising to nearby discoverers.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Starts advertising with the specified options.
    /// </summary>
    /// <param name="options">
    /// The <see cref="AdvertiseOptions"/> to configure advertising behavior.
    /// </param>
    /// <returns> A task representing the asynchronous operation.</returns>
    Task StartAdvertisingAsync(AdvertiseOptions options);

    /// <summary>
    /// Stops advertising this device to nearby discoverers.
    /// </summary>
    void StopAdvertising();
}