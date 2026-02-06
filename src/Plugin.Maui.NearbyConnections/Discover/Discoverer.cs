namespace Plugin.Maui.NearbyConnections.Discover;

sealed partial class Discoverer
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    bool _disposed;

    public Discoverer(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    public bool IsDiscovering
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                _nearbyConnections.Events.OnDiscoveringStateChanged(value, _nearbyConnections.TimeProvider.GetUtcNow());
            }
        }
    }

    public Task StartDiscoveringAsync()
        => PlatformStartDiscovering();

    public void StopDiscovering()
        => PlatformStopDiscovering();

    public void OnDiscoveryFailed()
        => IsDiscovering = false;
}