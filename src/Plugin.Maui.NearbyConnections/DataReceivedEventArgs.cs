namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="NearbyConnectionsEvents.DataReceived"/> event.
/// </summary>
/// <param name="nearbyDevice">The device that sent the payload.</param>
/// <param name="payload">The received payload.</param>
/// <param name="timestamp">The UTC timestamp when the data was received.</param>
public sealed class DataReceivedEventArgs(
    NearbyDevice nearbyDevice,
    NearbyPayload payload,
    DateTimeOffset timestamp) : NearbyConnectionsEventArgs(nearbyDevice, timestamp)
{
    /// <summary>
    /// Gets the received payload.
    /// </summary>
    public NearbyPayload Payload { get; } = payload;
}
