namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for discovering state change events.
/// </summary>
/// <param name="isDiscovering">Whether discovery is currently active.</param>
/// <param name="timestamp">The UTC timestamp when the state changed.</param>
public class DiscoveringStateChangedEventArgs(
    bool isDiscovering,
    DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// Gets a value indicating whether discovery is currently active.
    /// </summary>
    public bool IsDiscovering { get; } = isDiscovering;

    /// <summary>
    /// Gets the UTC timestamp when the state changed.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}
