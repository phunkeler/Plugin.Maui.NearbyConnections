using System.Collections.Specialized;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A bindable, live collection of rows projected from nearby devices, kept in sync by consuming
/// <see cref="INearbyDevices.Changes"/> and applying each change on a caller-supplied thread.
/// </summary>
/// <typeparam name="TRow">
/// The bound row type. Use <see cref="NearbyDevice"/> with <c>project: static device =&gt; device</c>
/// to bind devices directly, or a row type of your own when each row needs commands or state a
/// <see cref="NearbyDevice"/> snapshot cannot carry.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>Optional.</b> <see cref="INearby"/> itself has no thread affinity — it hands out immutable
/// snapshots and a change stream consumable from anywhere. This type exists only for consumers who
/// want a collection they can bind to XAML, and it is the one place in the library that assumes a
/// UI thread exists.
/// </para>
/// <para>
/// Construct one per view that needs it, and dispose it when the view goes away. Disposal cancels
/// the underlying enumeration. There is no event to unsubscribe from, and so nothing to leak.
/// </para>
/// <para>
/// The collection is read-only to its consumers — it mirrors what the session reports and cannot be
/// added to or removed from directly. It raises
/// <see cref="INotifyCollectionChanged.CollectionChanged"/>, so it binds straight to an
/// <c>ItemsSource</c>.
/// </para>
/// <para>
/// <b>A failed change stream is rethrown, not swallowed.</b> If the underlying stream faults, the
/// collection stops updating. Rather than freeze silently, it rethrows the fault as a
/// <see cref="NearbyException"/> through the <c>marshal</c> callback, so it surfaces on the
/// caller's own thread. Callbacks that throw — <c>project</c>, <c>filter</c>, <c>update</c> — fault
/// on that same thread directly, because <c>marshal</c> does not return their exceptions here.
/// </para>
/// <para>
/// <b>Rows are reused, not rebuilt.</b> A device that changes is handed to <c>update</c> rather than
/// re-projected, so a row keeps its own state — a spinner, a selection, a timestamp — across the
/// device's status transitions. A row is constructed once, when its device first passes
/// <c>filter</c>, and dropped when the device stops passing it or leaves the session.
/// </para>
/// </remarks>
/// <example>
/// Projecting a filtered subset onto row view models:
/// <code language="csharp">
/// // IDispatcher.Dispatch returns bool, so it is wrapped rather than passed as a method group.
/// public NearbyDeviceCollection&lt;DeviceRow&gt; Rows { get; }
///     = new(nearby,
///           marshal: action => dispatcher.Dispatch(action),
///           project: device => new DeviceRow(device, nearby),
///           filter: device => device.Status is NearbyDeviceStatus.Visible,
///           update: (row, device) => row.Update(device));
/// </code>
/// </example>
public class NearbyDeviceCollection<TRow> : IReadOnlyList<TRow>, INotifyCollectionChanged, IDisposable
{
    readonly ObservableCollection<TRow> _rows = [];
    readonly Dictionary<string, TRow> _byDeviceId = new(StringComparer.Ordinal);
    readonly List<string> _order = [];
    readonly CancellationTokenSource _cts = new();
    readonly Action<Action> _marshal;
    readonly INearby _nearby;
    readonly Func<NearbyDevice, bool> _filter;
    readonly Func<NearbyDevice, TRow> _project;
    readonly Action<TRow, NearbyDevice>? _update;

    int _disposeGuard;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDeviceCollection{TRow}"/> class and begins
    /// watching for device changes.
    /// </summary>
    /// <param name="nearby">The session to watch.</param>
    /// <param name="marshal">
    /// Runs an action where collection mutations are safe — in .NET MAUI,
    /// <see cref="IDispatcher.Dispatch(Action)"/>.
    /// <para>
    /// A callback rather than a dependency on <see cref="IDispatcher"/>, so this type stays
    /// platform-neutral: it compiles and is testable on the <c>net10.0</c> target, and all three
    /// public API baselines stay identical.
    /// </para>
    /// </param>
    /// <param name="project">Builds the row for a device that has just entered the collection.</param>
    /// <param name="filter">
    /// Selects which devices this collection shows, or <see langword="null"/> to show every device.
    /// Re-evaluated on every change, so a device that stops matching is removed.
    /// </param>
    /// <param name="update">
    /// Hands an existing row its device's newer snapshot. When <see langword="null"/>, a changed
    /// device is re-projected into a replacement row instead — correct for an immutable row type
    /// such as <see cref="NearbyDevice"/> itself, and wrong for a row carrying its own state.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nearby"/>, <paramref name="marshal"/>, or <paramref name="project"/> is
    /// <see langword="null"/>.
    /// </exception>
    public NearbyDeviceCollection(
        INearby nearby,
        Action<Action> marshal,
        Func<NearbyDevice, TRow> project,
        Func<NearbyDevice, bool>? filter = null,
        Action<TRow, NearbyDevice>? update = null)
    {
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(marshal);
        ArgumentNullException.ThrowIfNull(project);

        _nearby = nearby;
        _marshal = marshal;
        _project = project;
        _filter = filter ?? (static _ => true);
        _update = update;

        // Subscribe before seeding: the stream carries no history, so a change raised between the
        // seed and the subscription would be lost outright.
        var changes = _nearby.Devices.Changes.GetAsyncEnumerator(_cts.Token);

        _marshal(() =>
        {
            foreach (var device in _nearby.Devices)
            {
                Apply(new NearbyDeviceChange(NearbyDeviceChangeAction.Added, device));
            }
        });

        _ = WatchAsync(changes);
    }

    /// <summary>
    /// Occurs when rows are added, replaced, or removed.
    /// </summary>
    /// <remarks>
    /// Raised from inside the <c>marshal</c> callback supplied to the constructor, so every handler
    /// — including a XAML binding — runs on whatever thread the caller nominated as safe.
    /// </remarks>
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => ((INotifyCollectionChanged)_rows).CollectionChanged += value;
        remove => ((INotifyCollectionChanged)_rows).CollectionChanged -= value;
    }

    /// <summary>
    /// Gets the number of rows currently shown.
    /// </summary>
    /// <remarks>
    /// Read only on the thread that the constructor's <c>marshal</c> callback runs actions on.
    /// Rows are added and removed on that thread, so a count read from elsewhere is already
    /// stale by the time it is returned.
    /// </remarks>
    public int Count => _rows.Count;

    /// <summary>
    /// Gets the row at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the row to get.</param>
    /// <returns>The row at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside the bounds of the collection.
    /// </exception>
    /// <remarks>
    /// Read only on the thread that <c>marshal</c> runs actions on — the same rule as
    /// <see cref="Count"/> and <see cref="GetEnumerator"/>. Indexing from another thread can throw
    /// even for an index that was valid when it was chosen, because a removal can land in between.
    /// </remarks>
    public TRow this[int index] => _rows[index];

    /// <summary>
    /// Returns an enumerator that iterates through the rows currently shown.
    /// </summary>
    /// <returns>An enumerator over the collection.</returns>
    /// <remarks>
    /// Enumerate only on the thread that <c>marshal</c> runs actions on, the same rule that applies
    /// to any <see cref="ObservableCollection{T}"/> bound to a user interface — mutations arrive on
    /// that thread, so enumerating elsewhere can observe a torn collection.
    /// </remarks>
    public IEnumerator<TRow> GetEnumerator() => _rows.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => ((System.Collections.IEnumerable)_rows).GetEnumerator();

    /// <summary>
    /// Stops watching the session's change stream and releases the underlying enumeration.
    /// </summary>
    /// <remarks>
    /// Idempotent — calling this more than once performs no additional work.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) == 0)
        {
            // _cts is cancelled but deliberately not disposed, matching NearbyConnection
            // ._disconnectedCts. It is constructed with no delay, so it never allocated a Timer,
            // and Cancel() clears the registration list — what remains is a managed object with no
            // finalizer, collected with this collection. Disposing it would make the in-flight
            // WatchAsync enumeration throw ObjectDisposedException instead of observing
            // cancellation cleanly.
            _cts.Cancel();
        }

        // Nothing here holds an unmanaged handle, so this type declares no finalizer. The call is
        // still required: a derived type that introduces one would otherwise have to re-implement
        // IDisposable purely to suppress it (CA1816).
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Drains the session's change stream. Ends when the token is cancelled by
    /// <see cref="Dispose"/> — the reason this type has no unsubscribe step and cannot leak a
    /// watcher the way an event handler can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing awaits this task, so a fault it swallows is invisible: the collection stops updating
    /// and a bound view freezes at its last state with no diagnostic. A fault is therefore rethrown
    /// through <c>marshal</c>, which puts it on the thread the caller nominated — the same thread a
    /// throwing <c>project</c> or <c>update</c> already faults on, since <c>marshal</c> is
    /// fire-and-forget in .NET MAUI and those callbacks never return here.
    /// </para>
    /// <para>
    /// The cancellation catch is filtered on this collection's own token. An
    /// <see cref="OperationCanceledException"/> from any other source is a fault, not a teardown,
    /// and takes the rethrow path.
    /// </para>
    /// </remarks>
    async Task WatchAsync(IAsyncEnumerator<NearbyDeviceChange> changes)
    {
        try
        {
            while (await changes.MoveNextAsync().ConfigureAwait(false))
            {
                var change = changes.Current;
                _marshal(() => Apply(change));
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Disposed.
        }
        catch (Exception ex)
        {
            _marshal(() => throw new NearbyException(
                "The nearby device collection stopped updating because its change stream failed. "
                + "Rows will no longer reflect the session.",
                ex));
        }
        finally
        {
            await changes.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies one change. Runs inside <c>marshal</c>.
    /// </summary>
    /// <remarks>
    /// A device that no longer passes <c>filter</c> is removed whatever the change action says, so
    /// a status transition out of the filtered set reads as a removal rather than leaving a stale
    /// row behind.
    /// </remarks>
    void Apply(NearbyDeviceChange change)
    {
        var device = change.Device;

        if (change.Action is NearbyDeviceChangeAction.Removed || !_filter(device))
        {
            Remove(device.Id);
            return;
        }

        if (!_byDeviceId.TryGetValue(device.Id, out var existing))
        {
            var row = _project(device);

            _byDeviceId[device.Id] = row;
            _order.Add(device.Id);
            _rows.Add(row);
            return;
        }

        if (_update is not null)
        {
            _update(existing, device);
            return;
        }

        // No updater: the row carries no state of its own, so a replacement is the update. The
        // indexer assignment raises Replace, which lets a bound row refresh in place.
        var replacement = _project(device);
        var index = _order.IndexOf(device.Id);

        _byDeviceId[device.Id] = replacement;
        _rows[index] = replacement;
    }

    void Remove(string deviceId)
    {
        if (!_byDeviceId.Remove(deviceId, out var row))
        {
            return;
        }

        _order.Remove(deviceId);
        _rows.Remove(row);
    }
}
