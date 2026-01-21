namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for advertising state change events.
/// </summary>
/// <param name="isAdvertising">Whether advertising is currently active.</param>
/// <param name="timestamp">The UTC timestamp when the state changed.</param>
public class AdvertisingStateChangedEventArgs(
    bool isAdvertising,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// Gets a value indicating whether advertising is currently active.
    /// </summary>
    public bool IsAdvertising { get; } = isAdvertising;

    /// <summary>
    /// Gets the UTC timestamp when the state changed.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}
