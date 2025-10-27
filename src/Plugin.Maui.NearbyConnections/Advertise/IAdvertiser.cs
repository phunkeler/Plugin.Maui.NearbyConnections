namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Represents the advertising capability that makes this device discoverable to nearby devices.
/// </summary>
/// <remarks>
/// Advertising broadcasts this device's presence to nearby discoverers, allowing them to initiate
/// connections. This is one half of the peer-to-peer discovery process - devices must either
/// advertise or discover (or both) to establish connections.
/// <para>
/// The advertiser should be disposed when no longer needed to stop broadcasting and release resources.
/// </para>
/// </remarks>
public interface IAdvertiser : IDisposable
{
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