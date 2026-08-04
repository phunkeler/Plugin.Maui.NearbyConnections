namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Thrown when a remote device does not answer a connection request within
/// <see cref="NearbyConnectionsOptions.InvitationTimeout"/>.
/// </summary>
/// <remarks>
/// Means the request went out and nothing came back — the peer never accepted or rejected it, most
/// often because it moved out of range mid-handshake or the user never answered the prompt. The
/// device returns to <see cref="NearbyDeviceStatus.Visible"/>, so retrying is reasonable.
/// </remarks>
public sealed class NearbyConnectionTimeoutException : NearbyConnectionsException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyConnectionTimeoutException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyConnectionTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
