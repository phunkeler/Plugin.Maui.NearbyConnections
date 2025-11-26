namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Base exception for nearby connections operations.
/// </summary>
public class NearbyConnectionsException : Exception
{
    /// <summary>
    /// Gets the nearby connections options associated with the exception.
    /// </summary>
    public NearbyConnectionsOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsException"/> class.
    /// </summary>
    /// <param name="options">The nearby connections options associated with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyConnectionsException(
        NearbyConnectionsOptions options,
        string message) : base(message)
    {
        Options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsException"/> class.
    /// </summary>
    /// <param name="options">The nearby connections options associated with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyConnectionsException(
        NearbyConnectionsOptions options,
        string message,
        Exception innerException) : base(message, innerException)
    {
        Options = options;
    }
}