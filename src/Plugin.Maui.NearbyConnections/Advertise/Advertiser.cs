namespace Plugin.Maui.NearbyConnections.Advertise;

internal sealed partial class Advertiser
{
    readonly NearbyConnectionsImplementation _nearbyConnections;

    bool _disposed;
    bool _isAdvertising;

    internal Advertiser(NearbyConnectionsImplementation nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _nearbyConnections = nearbyConnections;
    }

    internal bool IsAdvertising
    {
        get => _isAdvertising;
        private set
        {
            if (_isAdvertising != value)
            {
                _isAdvertising = value;
                _nearbyConnections.Events.OnAdvertisingStateChanged(value, _nearbyConnections.TimeProvider.GetUtcNow());
            }
        }
    }

    internal Task StartAdvertisingAsync(string displayName)
        => PlatformStartAdvertising(displayName);

    internal void StopAdvertising()
        => PlatformStopAdvertising();

    internal void OnAdvertisingFailed()
        => IsAdvertising = false;
}