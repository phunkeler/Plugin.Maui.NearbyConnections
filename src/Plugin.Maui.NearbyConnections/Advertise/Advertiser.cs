namespace Plugin.Maui.NearbyConnections.Advertise;

sealed partial class Advertiser
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    bool _disposed;

    public Advertiser(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    public bool IsAdvertising
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                _nearbyConnections.Events.OnAdvertisingStateChanged(value, _nearbyConnections.TimeProvider.GetUtcNow());
            }
        }
    }

    public Task StartAdvertisingAsync()
        => PlatformStartAdvertising();

    public void StopAdvertising()
        => PlatformStopAdvertising();

    public void OnAdvertisingFailed()
        => IsAdvertising = false;
}