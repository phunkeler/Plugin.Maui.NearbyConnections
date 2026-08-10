namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when a remote device does not answer a connection request within
/// <see cref="NearbyOptions.InvitationTimeout"/>.
/// </summary>
/// <remarks>
/// This exception indicates that the request was sent but no response was received: the remote
/// device neither accepted nor rejected it. This most often occurs when the device moves out of
/// range during the handshake, or when the user does not answer the prompt. The device returns to
/// the <see cref="NearbyDeviceStatus.Visible"/> state, so retrying the connection is a reasonable
/// response.
/// </remarks>
public sealed class NearbyConnectionTimeoutException : NearbyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyConnectionTimeoutException(string message) : base(message)
    {
    }
}
