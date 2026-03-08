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

#pragma warning disable CA1822, S2325
    public Task StartAdvertisingAsync()
#pragma warning restore CA1822, S2325
        => PlatformStartAdvertising();

#pragma warning disable CA1822, S2325
    public void StopAdvertising()
#pragma warning restore CA1822, S2325
        => PlatformStopAdvertising();

    public void OnAdvertisingFailed()
        => IsAdvertising = false;
}