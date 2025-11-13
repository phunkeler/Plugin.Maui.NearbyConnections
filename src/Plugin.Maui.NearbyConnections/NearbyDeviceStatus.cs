namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents the connection status of a nearby device.
/// </summary>
/// <remarks>
/// <para>
/// This enum unifies the connection state models across platforms:
/// </para>
/// <para>
/// **Android (Nearby Connections API)**:
/// - Discovered: OnEndpointFound callback
/// - Invited: After SendInvitation or OnConnectionInitiated
/// - Connected: OnConnectionResult with success status
/// - Disconnected: OnDisconnected callback or connection failure
/// </para>
/// <para>
/// **iOS (MultipeerConnectivity)**:
/// - Discovered: FoundPeer callback
/// - Invited: DidReceiveInvitationFromPeer callback + MCSessionState.Connecting
/// - Connected: MCSessionState.Connected
/// - Disconnected: MCSessionState.NotConnected or invitation declined
/// </para>
/// </remarks>
public enum NearbyDeviceStatus
{
    /// <summary>
    /// Device has been disconnected or the invitation was declined.
    /// </summary>
    Disconnected,

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
    Connected
}