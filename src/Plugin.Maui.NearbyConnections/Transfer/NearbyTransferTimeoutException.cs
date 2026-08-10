namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when a file transfer stalls and no progress is reported within
/// <see cref="NearbyOptions.TransferInactivityTimeout"/>.
/// </summary>
public sealed class NearbyTransferTimeoutException : NearbyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyTransferTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyTransferTimeoutException(string message) : base(message)
    {
    }
}
