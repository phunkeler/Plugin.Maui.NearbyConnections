namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when a connection request goes unanswered within
/// <see cref="NearbyOptions.InvitationTimeout"/>.
/// </summary>
/// <remarks>
/// The request was sent but the remote device neither accepted nor rejected it — most often because
/// it moved out of range mid-handshake, or its user never answered the prompt. The device returns to
/// <see cref="NearbyDeviceStatus.Visible"/>, so retrying the connection is a reasonable response.
/// </remarks>
/// <param name="message">The error message that explains the reason for the exception.</param>
public sealed class NearbyConnectionTimeoutException(string message) : NearbyException(message)
{
}
