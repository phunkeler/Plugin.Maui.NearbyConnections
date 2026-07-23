namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Base exception for nearby connections operations.
/// </summary>
/// <remarks>
/// This class is intentionally non-sealed to support the sealed subclasses
/// <see cref="NearbyAdvertisingException"/>, <see cref="NearbyDiscoveryException"/>, and
/// <see cref="NearbyTransferTimeoutException"/> shipped with this library. Direct consumer
/// subclassing is a supported extension contract.
/// </remarks>
public class NearbyDevicesException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDevicesException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyDevicesException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDevicesException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyDevicesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a file transfer stalls and no progress is received within the configured
/// inactivity timeout.
/// </summary>
public sealed class NearbyTransferTimeoutException : NearbyDevicesException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyTransferTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyTransferTimeoutException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyTransferTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyTransferTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}