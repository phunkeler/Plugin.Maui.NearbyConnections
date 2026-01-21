namespace Plugin.Maui.NearbyConnections.Discover;

internal sealed partial class Discoverer
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    bool _disposed;
    bool _isDiscovering;

    internal Discoverer(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal bool IsDiscovering
    {
        get => _isDiscovering;
        private set
        {
            if (_isDiscovering != value)
            {
                _isDiscovering = value;
                _nearbyConnections.Events.OnDiscoveringStateChanged(value, _nearbyConnections.TimeProvider.GetUtcNow());
            }
        }
    }

    internal Task StartDiscoveringAsync()
        => PlatformStartDiscovering();

    internal void StopDiscovering()
        => PlatformStopDiscovering();

    internal void OnDiscoveryFailed()
        => IsDiscovering = false;
}