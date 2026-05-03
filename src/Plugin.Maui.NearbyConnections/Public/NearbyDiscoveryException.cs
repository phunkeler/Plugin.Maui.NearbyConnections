namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Exception thrown during discovery operations.
/// </summary>
public class NearbyDiscoveryException : NearbyConnectionsException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDiscoveryException"/> class.
    /// </summary>
    /// <param name="options">The nearby connections options associated with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyDiscoveryException(
        NearbyConnectionsOptions options,
        string message) : base(options, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDiscoveryException"/> class.
    /// </summary>
    /// <param name="options">The nearby connections options associated with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyDiscoveryException(
        NearbyConnectionsOptions options,
        string message,
        Exception innerException) : base(options, message, innerException)
    {
    }
}