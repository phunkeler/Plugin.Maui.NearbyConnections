namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies the Android connection type used when advertising or discovering nearby devices.
/// </summary>
/// <remarks>
/// Maps to <c>Android.Gms.Nearby.Connection.ConnectionType</c> constants.
/// This option has no effect on iOS.
/// </remarks>
public enum NearbyConnectionType
{
    /// <summary>
    /// Attempts to balance connection speed and power usage.
    /// </summary>
    Balanced = 0,

    /// <summary>
    /// Optimises for connection speed at the cost of higher power usage.
    /// </summary>
    Disruptive = 1,

    /// <summary>
    /// Optimises for low power usage at the cost of lower throughput.
    /// </summary>
    NonDisruptive = 2,
}
