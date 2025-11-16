namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents the connection status of a nearby device.
/// </summary>
public enum NearbyDeviceStatus
{
    /// <summary>
    /// Initial state or status cannot be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Device has been discovered but no connection has been initiated.
    /// </summary>
    Discovered,

    /// <summary>
    /// A connection invitation has been sent to the device and is awaiting a response,
    /// or the device is in a connecting state (MCSessionState.Connecting on iOS).
    /// </summary>
    Invited,

    /// <summary>
    /// Device is connected and ready for communication.
    /// </summary>
    Connected,

    /// <summary>
    /// Device has been disconnected or the invitation was declined.
    /// </summary>
    Disconnected,
}