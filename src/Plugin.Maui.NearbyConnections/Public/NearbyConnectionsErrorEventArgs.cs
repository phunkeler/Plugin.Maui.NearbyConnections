namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for error events.
/// </summary>
/// <param name="operation">The operation that failed (e.g., "Advertising", "Discovery", etc...).</param>
/// <param name="errorMessage">The error message.</param>
/// <param name="timestamp">The UTC timestamp when the error occurred.</param>
/// <param name="device">The device associated with the error, if applicable.</param>
public class NearbyConnectionsErrorEventArgs(
    string operation,
    string errorMessage,
    DateTimeOffset timestamp,
    NearbyDevice? device = null) : EventArgs
{
    /// <summary>
    /// Gets the operation that failed.
    /// </summary>
    public string Operation { get; } = operation;

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string ErrorMessage { get; } = errorMessage;

    /// <summary>
    /// Gets the UTC timestamp when the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// Gets the device associated with the error, or <see langword="null"/> if
    /// the error is not specific to a nearby device.
    /// </summary>
    public NearbyDevice? Device { get; } = device;
}