namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for the <see cref="NearbyConnectionsEvents.IncomingTransferProgress"/> event.
/// </summary>
/// <param name="nearbyDevice">The device sending the transfer.</param>
/// <param name="progress">The current transfer progress.</param>
/// <param name="timestamp">The UTC timestamp of this progress update.</param>
public sealed class DataTransferProgressEventArgs(
    NearbyDevice nearbyDevice,
    NearbyTransferProgress progress,
    DateTimeOffset timestamp) : NearbyConnectionsEventArgs(nearbyDevice, timestamp)
{
    /// <summary>
    /// Gets the current transfer progress.
    /// </summary>
    public NearbyTransferProgress Progress { get; } = progress;
}
