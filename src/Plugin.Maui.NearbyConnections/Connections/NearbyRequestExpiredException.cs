namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception thrown when <see cref="NearbyConnectionRequest.AcceptAsync(CancellationToken)"/>
/// or <see cref="NearbyConnectionRequest.RejectAsync(CancellationToken)"/> is called on a request
/// that is no longer outstanding — it expired, or it was already answered.
/// </summary>
/// <remarks>
/// The request's <see cref="NearbyConnectionRequest.Expired"/> task is the proactive signal — a
/// view awaits it to dismiss its prompt. No signal beats every race, so this exception is the
/// backstop for the answer that arrives just after the expiry wins.
/// </remarks>
/// <param name="message">The message that describes the error.</param>
public sealed class NearbyRequestExpiredException(string message) : NearbyException(message)
{
}
