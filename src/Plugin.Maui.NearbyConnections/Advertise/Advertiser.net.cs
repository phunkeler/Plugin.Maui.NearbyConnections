namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
internal partial class Advertiser
{
    /// <summary>
    /// Starts advertising for nearby connections.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task PlatformStartAdvertising(AdvertiseOptions options)
        => throw new NotImplementedException("Platform-specific advertising start logic must be implemented.");

    /// <summary>
    /// Stops advertising for nearby connections.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public void PlatformStopAdvertising()
        => throw new NotImplementedException("Platform-specific advertising stop logic must be implemented.");
}