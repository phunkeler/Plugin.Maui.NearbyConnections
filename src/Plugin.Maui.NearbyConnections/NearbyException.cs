namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when a nearby connections operation fails.
/// </summary>
/// <remarks>
/// This is the base class for every exception raised by this library. It is deliberately not
/// sealed, both to support the derived types shipped with the library and to allow consumers to
/// derive their own. Catch this type to handle any failure originating from the plugin.
/// </remarks>
/// <seealso cref="NearbyAdvertisingException"/>
/// <seealso cref="NearbyDiscoveryException"/>
/// <seealso cref="NearbyConnectionTimeoutException"/>
/// <seealso cref="NearbyTransferTimeoutException"/>
public class NearbyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}