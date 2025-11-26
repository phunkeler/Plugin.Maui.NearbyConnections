namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides data for error events.
/// </summary>
/// <param name="operation">The operation that failed (e.g., "Advertising", "Discovery", etc...).</param>
/// <param name="errorMessage">The error message.</param>
/// <param name="timestamp">The UTC timestamp when the error occurred.</param>
public class NearbyConnectionsErrorEventArgs(
    string operation,
    string errorMessage,
    DateTimeOffset timestamp) : EventArgs
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
}