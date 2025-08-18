namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Advertising state change event arguments
/// </summary>
public class AdvertisingStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Indicates whether advertising is currently active.
    /// </summary>
    public bool IsAdvertising { get; init; }

    /// <summary>
    /// Gets the timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}