namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Event arguments for discovering state changes.
/// </summary>
public class DiscoveringStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Indicates whether discovering is currently active.
    /// </summary>
    public bool IsDiscovering { get; init; }

    /// <summary>
    /// Gets the timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}