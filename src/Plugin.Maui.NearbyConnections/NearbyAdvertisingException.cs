namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Exception thrown during advertising operations.
/// </summary>
public class NearbyAdvertisingException : NearbyConnectionsException
{
    /// <summary>
    /// Gets the display name being advertised.
    /// </summary>
    public string DisplayName { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyAdvertisingException"/> class.
    /// </summary>
    /// <param name="displayName">The display name being advertised.</param>
    /// <param name="options">The nearby connections options associated with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyAdvertisingException(
        string displayName,
        NearbyConnectionsOptions options,
        string message) : base(options, message)
    {
        DisplayName = displayName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyAdvertisingException"/> class.
    /// </summary>
    /// <param name="displayName">The display name being advertised.</param>
    /// <param name="options">The nearby connections options associated with the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyAdvertisingException(
        string displayName,
        NearbyConnectionsOptions options,
        string message,
        Exception innerException) : base(options, message, innerException)
    {
        DisplayName = displayName;
    }
}