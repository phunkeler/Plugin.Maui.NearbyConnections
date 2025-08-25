namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Advertising state change event arguments
/// </summary>
public class AdvertisingStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Indicates the current advertising state (After the internal state change )
    /// </summary>
    public IAdvertisingState CurrentState { get; init; }

    /// <summary>
    /// Gets the previous advertising state.
    /// </summary>
    public IAdvertisingState PreviousState { get; init; }
}