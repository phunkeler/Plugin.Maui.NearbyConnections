namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Event representing a found nearby connection.
/// </summary>
public class NearbyConnectionFound : INearbyConnectionsEvent
{
    /// <summary>
    /// The ID of the discovered endpoint.
    /// </summary>
    public string EndpointId { get; }

    /// <summary>
    /// The name of the discovered endpoint.
    /// </summary>
    public string EndpointName { get; }

    /// <summary>
    /// The unique identifier for the event.
    /// </summary>
    public string EventId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The timestamp when the event was created.
    /// </summary>
    public DateTimeOffset Created => DateTimeOffset.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionFound"/> class.
    /// </summary>
    /// <param name="endpointId">The ID of the discovered endpoint.</param>
    /// <param name="endpointName">The name of the discovered endpoint.</param>
    public NearbyConnectionFound(string endpointId, string endpointName)
    {
        ArgumentNullException.ThrowIfNull(endpointId);
        ArgumentNullException.ThrowIfNull(endpointName);

        EndpointId = endpointId;
        EndpointName = endpointName;
    }
}