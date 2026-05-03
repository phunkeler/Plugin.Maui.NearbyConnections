namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="INearbyConnections.DeviceLost"/> event.
/// </summary>
/// <param name="nearbyDevice">The device that is no longer visible.</param>
/// <param name="timestamp">The UTC timestamp when the device was lost.</param>
public sealed class DeviceLostEventArgs(
    NearbyDevice nearbyDevice,
    DateTimeOffset timestamp) : NearbyConnectionsEventArgs(nearbyDevice, timestamp);
