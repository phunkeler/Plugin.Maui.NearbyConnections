namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="INearbyConnections.DeviceDisconnected"/> event.
/// </summary>
/// <param name="nearbyDevice">The device that disconnected.</param>
/// <param name="timestamp">The UTC timestamp when the disconnection occurred.</param>
public sealed class DeviceDisconnectedEventArgs(
    NearbyDevice nearbyDevice,
    DateTimeOffset timestamp) : NearbyConnectionsEventArgs(nearbyDevice, timestamp);
