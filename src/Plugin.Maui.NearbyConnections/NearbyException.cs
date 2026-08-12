namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when a nearby connectivity operation fails.
/// </summary>
/// <remarks>
/// Every exception this library raises derives from this type, including the ones consumers are
/// expected to catch by name (<see cref="NearbyAdvertisingException"/>,
/// <see cref="NearbyDiscoveryException"/>, and the rest below). It is deliberately not sealed, so
/// that consumers can derive their own exception types from it. Catch <see cref="NearbyException"/>
/// to handle any failure that originates in the plugin without enumerating every derived type.
/// </remarks>
/// <seealso cref="NearbyAdvertisingException"/>
/// <seealso cref="NearbyDiscoveryException"/>
/// <seealso cref="NearbyConnectionTimeoutException"/>
/// <seealso cref="NearbyTransferException"/>
/// <seealso cref="NearbyTransferTimeoutException"/>
public class NearbyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyException"/> class.
    /// </summary>
    /// <param name="message">The message that explains the reason for the exception.</param>
    public NearbyException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyException"/> class.
    /// </summary>
    /// <param name="message">The message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public NearbyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}