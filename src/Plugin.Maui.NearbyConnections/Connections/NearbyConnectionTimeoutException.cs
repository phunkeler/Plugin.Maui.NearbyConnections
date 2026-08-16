namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when a connection is not established within its deadline —
/// <see cref="NearbyOptions.ConnectTimeout"/> for
/// <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/>, or
/// <see cref="NearbyOptions.AcceptTimeout"/> for
/// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// The handshake started but never reached a terminal result — most often because the remote device
/// moved out of range mid-handshake, or, when connecting, because its user never answered the
/// prompt. The device returns to <see cref="NearbyDeviceStatus.Visible"/>, so retrying the
/// connection is a reasonable response.
/// </remarks>
/// <param name="message">The error message that explains the reason for the exception.</param>
public sealed class NearbyConnectionTimeoutException(string message) : NearbyException(message)
{
}