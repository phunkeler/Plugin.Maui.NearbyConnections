namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event representing a found nearby connection.
/// </summary>
public class NearbyConnectionLost : INearbyConnectionsEvent
{
    readonly TimeProvider _timeProvider;
    /// <summary>
    /// The ID of the discovered endpoint.
    /// </summary>
    public string EndpointId { get; }

    /// <summary>
    /// The unique identifier for the event.
    /// </summary>
    public string EventId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The timestamp when the event was created.
    /// </summary>
    public DateTimeOffset Created { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionLost"/> class.
    /// </summary>
    /// <param name="endpointId">The ID of the discovered endpoint.</param>
    /// <param name="timeProvider">The to,e [rpvoder.</param>
    public NearbyConnectionLost(
        TimeProvider timeProvider,
        string endpointId)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(endpointId);

        _timeProvider = timeProvider;
        EndpointId = endpointId;
        Created = _timeProvider.GetUtcNow();
    }
}
