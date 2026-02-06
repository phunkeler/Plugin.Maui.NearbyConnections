namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="NearbyConnectionsEvents.ConnectionResponded"/> event.
/// </summary>
public class NearbyDeviceRespondedEventArgs(
    NearbyDevice nearbyDevice,
    DateTimeOffset timestamp,
    bool accepted) : NearbyConnectionsEventArgs(nearbyDevice, timestamp)
{
    /// <summary>
    /// Gets a value indicating whether the connection request was accepted.
    /// </summary>
    public bool Accepted { get; } = accepted;
}