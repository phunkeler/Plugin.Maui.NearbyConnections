namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="INearbyConnections.ConnectionRequested"/> event.
/// </summary>
/// <param name="nearbyDevice">The device requesting a connection.</param>
/// <param name="timestamp">The UTC timestamp when the request was received.</param>
public sealed class ConnectionRequestedEventArgs(
    NearbyDevice nearbyDevice,
    DateTimeOffset timestamp) : NearbyConnectionsEventArgs(nearbyDevice, timestamp);
