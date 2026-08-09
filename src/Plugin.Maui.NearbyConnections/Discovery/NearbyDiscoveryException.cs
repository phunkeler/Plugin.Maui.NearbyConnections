namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The exception that is thrown when the platform fails to start or continue discovery.
/// </summary>
/// <remarks>
/// This exception surfaces from
/// <see cref="INearby.StartDiscoveryAsync(CancellationToken)"/>. Common causes are
/// missing permissions, a disabled radio, or an invalid
/// <see cref="NearbyOptions.ServiceId"/>.
/// </remarks>
public sealed class NearbyDiscoveryException : NearbyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public NearbyDiscoveryException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NearbyDiscoveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}