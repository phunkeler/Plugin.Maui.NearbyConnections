namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="INearbyConnections.DeviceFound"/> event.
/// </summary>
/// <param name="nearbyDevice">The device that was discovered.</param>
/// <param name="timestamp">The UTC timestamp when the device was found.</param>
public sealed class DeviceFoundEventArgs(
    NearbyDevice nearbyDevice,
    DateTimeOffset timestamp) : NearbyConnectionsEventArgs(nearbyDevice, timestamp);
