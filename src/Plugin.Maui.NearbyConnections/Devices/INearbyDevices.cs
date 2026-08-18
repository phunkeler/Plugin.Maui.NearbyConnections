namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The set of devices known to an <see cref="INearby"/>, together with the stream of changes to
/// that set.
/// </summary>
/// <remarks>
/// <para>
/// <b>Callable from any thread.</b> Enumerating the list itself reads an immutable snapshot taken
/// at the moment of the call, so it never throws for concurrent modification. <see cref="Changes"/>
/// is delivered on a thread-pool thread — never the UI thread, and never the platform SDK's own
/// callback thread — so a consumer binding to a user interface marshals it itself, or constructs a
/// <see cref="NearbyDeviceCollection{TRow}"/>, which does that on its behalf.
/// </para>
/// <para>
/// This is a noun-phrase interface, which the Framework Design Guidelines discourage in general —
/// but noun names are the established convention for a collection type
/// (<see cref="IReadOnlyList{T}"/>, <see cref="ICollection{T}"/>), which is what this is.
/// </para>
/// </remarks>
public interface INearbyDevices : IReadOnlyList<NearbyDevice>
{
    /// <summary>
    /// Gets the stream of changes to this collection.
    /// </summary>
    /// <value>
    /// An <see cref="IAsyncEnumerable{T}"/> that yields one <see cref="NearbyDeviceChange"/> per
    /// change, until the enumeration is cancelled.
    /// </value>
    /// <remarks>
    /// <para>
    /// <b>Broadcast, not replayed.</b> Every enumeration gets its own feed of every change raised
    /// while it runs, independently of any other enumeration — unlike
    /// <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/>, which is single-consumer
    /// because each payload must be handled exactly once. A change raised before an enumeration
    /// started is never delivered to it: read the list for current state, then watch this for what
    /// happens next.
    /// </para>
    /// <para>
    /// Each enumeration buffers independently, so a slow consumer neither blocks another enumeration
    /// nor blocks the platform callback that raised the change. Ending the enumeration — cancelling
    /// the token or breaking out of the loop — is the only cleanup required.
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