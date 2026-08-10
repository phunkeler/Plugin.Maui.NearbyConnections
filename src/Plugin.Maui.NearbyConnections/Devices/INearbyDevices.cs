namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The devices known to an <see cref="INearby"/>: the current set, and the stream of changes to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread-agnostic.</b> Enumerating this collection yields an immutable snapshot taken at the
/// moment of the call, so it is safe to read from any thread and never throws for concurrent
/// modification. <see cref="Changes"/> is delivered on whatever thread the platform callback
/// arrived on; a consumer that binds the result to a user interface marshals it itself, or uses
/// <see cref="NearbyDeviceCollection"/>, which does that.
/// </para>
/// <para>
/// This is a noun-phrase interface, which the Framework Design Guidelines discourage in general —
/// but it is a collection, where noun names are the convention
/// (<see cref="IReadOnlyList{T}"/>, <see cref="ICollection{T}"/>).
/// </para>
/// </remarks>
public interface INearbyDevices : IReadOnlyList<NearbyDevice>
{
    /// <summary>
    /// Gets the stream of changes to this collection.
    /// </summary>
    /// <value>
    /// An <see cref="IAsyncEnumerable{T}"/> that yields one <see cref="NearbyDeviceChange"/> per
    /// change until the enumeration is cancelled.
    /// </value>
    /// <remarks>
    /// <para>
    /// <b>Broadcast, and without replay.</b> Every enumeration receives every change that occurs
    /// while it is running, independently of the others — unlike
    /// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/>, which is single-consumer
    /// because each payload must be handled exactly once. Changes that occurred before an
    /// enumeration started are not replayed: read this collection for the current state, then watch
    /// for what happens next.
    /// </para>
    /// <para>
    /// A slow consumer does not block the others or the platform callback: each enumeration buffers
    /// independently. The enumeration ends when its cancellation token is cancelled.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example watches for devices that request a connection.
    /// <code language="csharp">
    /// await foreach (var change in nearby.Devices.Changes.WithCancellation(cancellationToken))
    /// {
    ///     if (change.Device.Status is NearbyDeviceStatus.RequestReceived)
    ///     {
    ///         // show the accept/decline row
    ///     }
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<NearbyDeviceChange> Changes { get; }
}
