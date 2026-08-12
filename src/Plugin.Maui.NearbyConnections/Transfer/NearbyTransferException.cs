namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when sending or receiving a payload fails for a reason other than a
/// timeout.
/// </summary>
/// <remarks>
/// This exception surfaces from <see cref="NearbyConnection.SendAsync(byte[], CancellationToken)"/>
/// and its overloads. Common causes are the remote device disconnecting mid-transfer, the platform
/// rejecting the send, or an I/O failure while reading a file to send. For a transfer that stalls
/// instead of failing outright, see <see cref="NearbyTransferTimeoutException"/>.
/// </remarks>
/// <seealso cref="NearbyTransferTimeoutException"/>
public sealed class NearbyTransferException : NearbyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyTransferException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyTransferException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyTransferException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyTransferException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
