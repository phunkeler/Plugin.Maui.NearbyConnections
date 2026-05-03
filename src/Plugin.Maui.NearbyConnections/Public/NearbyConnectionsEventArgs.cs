namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for nearby connections events.
/// </summary>
/// <param name="nearbyDevice">The nearby device associated with the event.</param>
/// <param name="timestamp">The UTC timestamp when the event occurred.</param>
public class NearbyConnectionsEventArgs(
    NearbyDevice nearbyDevice,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// Gets the nearby device associated with the event.
    /// </summary>
    public NearbyDevice NearbyDevice { get; } = nearbyDevice;

    /// <summary>
    /// Gets the UTC timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}