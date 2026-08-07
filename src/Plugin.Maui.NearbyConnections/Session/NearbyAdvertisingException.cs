namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when the platform fails to start or continue advertising.
/// </summary>
/// <remarks>
/// This exception surfaces from
/// <see cref="INearbySession.StartAdvertisingAsync(CancellationToken)"/>. Common causes are
/// missing permissions, a disabled radio, or an invalid
/// <see cref="NearbyConnectionsOptions.ServiceId"/>.
/// </remarks>
public sealed class NearbyAdvertisingException : NearbyConnectionsException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyAdvertisingException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyAdvertisingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyAdvertisingException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyAdvertisingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}