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

#pragma warning disable CA1822, S2325
    public Task StartDiscoveringAsync()
#pragma warning restore CA1822, S2325
        => PlatformStartDiscovering();

#pragma warning disable CA1822, S2325
    public void StopDiscovering()
#pragma warning restore CA1822, S2325
        => PlatformStopDiscovering();

    public void OnDiscoveryFailed()
        => IsDiscovering = false;
}