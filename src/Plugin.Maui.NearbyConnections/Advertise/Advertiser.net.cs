namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser
{
    public Task PlatformStartAdvertising(AdvertiseOptions options)
        => throw new NotImplementedException("Platform-specific advertising start logic must be implemented.");

    public void PlatformStopAdvertising()
        => throw new NotImplementedException("Platform-specific advertising stop logic must be implemented.");
}