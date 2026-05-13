namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Base exception for nearby connections operations.
/// </summary>
/// <remarks>
/// This class is intentionally non-sealed to support the sealed subclasses
/// <see cref="NearbyAdvertisingException"/> and <see cref="NearbyDiscoveryException"/> shipped
/// with this library. Direct consumer subclassing is a supported extension contract.
/// </remarks>
public class NearbyConnectionsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyConnectionsException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyConnectionsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}