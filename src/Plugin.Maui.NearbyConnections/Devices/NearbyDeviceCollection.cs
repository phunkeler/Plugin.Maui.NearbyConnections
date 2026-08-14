using System.Collections.Specialized;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A bindable, live collection of nearby devices, kept in sync by consuming
/// <see cref="INearbyDevices.Changes"/> and applying each change on a caller-supplied thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>Optional.</b> <see cref="INearby"/> itself has no thread affinity — it hands out immutable
/// snapshots and a change stream consumable from anywhere. This type exists only for consumers who
/// want a collection they can bind to XAML, and it is the one place in the library that assumes a
/// UI thread exists.
/// </para>
/// <para>
/// Construct one per view that needs it, and dispose it when the view goes away. Disposal cancels
/// the underlying enumeration; there is no event to unsubscribe from and so nothing to leak.
/// </para>
/// <para>
/// The collection is read-only to its consumers — it mirrors what the session reports and cannot be
/// added to or removed from directly. It raises
/// <see cref="INotifyCollectionChanged.CollectionChanged"/>, so it binds straight to an
/// <c>ItemsSource</c>.
/// </para>
/// </remarks>
/// <example>
/// In a .NET MAUI ViewModel:
/// <code language="csharp">
/// public NearbyDeviceCollection Devices { get; }
///     = new(nearby, marshal: Dispatcher.Dispatch);
///
/// // then bind straight to it: ItemsSource="{Binding Devices}"
/// </code>
/// </example>
public sealed class NearbyDeviceCollection : IReadOnlyList<NearbyDevice>, INotifyCollectionChanged, IDisposable
{
    readonly ObservableCollection<NearbyDevice> _devices = [];
    readonly CancellationTokenSource _cts = new();
    readonly Action<Action> _marshal;
    readonly INearby _nearby;

    int _disposeGuard;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDeviceCollection"/> class and begins
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nearby"/> or <paramref name="marshal"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// A device is removed only once the platform reports it lost, never before. Neither platform
    /// reliably reports every departure, so a device that moved out of range can linger until
    /// discovery restarts — evicting on a timer instead would need a periodic "still here" signal
    /// that <see cref="INearbyDevices.Changes"/> does not carry, and would delete devices that are
    /// still present.
    /// </remarks>
    public NearbyDeviceCollection(INearby nearby, Action<Action> marshal)
    {
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(marshal);

        _nearby = nearby;
        _marshal = marshal;

        var changes = _nearby.Devices.Changes.GetAsyncEnumerator(_cts.Token);

        _marshal(() =>
        {
            foreach (var device in _nearby.Devices)
            {
                _devices.Add(device);
            }
        });

        _ = WatchAsync(changes);
    }

    /// <summary>
    /// Occurs when devices are added, replaced, or removed.
    /// </summary>
    /// <remarks>
    /// Raised from inside the <c>marshal</c> callback supplied to the constructor, so every handler
    /// — including a XAML binding — runs on whatever thread the caller nominated as safe.
    /// </remarks>
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => ((INotifyCollectionChanged)_devices).CollectionChanged += value;
        remove => ((INotifyCollectionChanged)_devices).CollectionChanged -= value;
    }

    /// <summary>
    /// Gets the number of devices currently known.
    /// </summary>
    /// <remarks>
    /// Read only on the thread that the constructor's <c>marshal</c> callback runs actions on.
    /// Devices are added and removed on that thread, so a count read from elsewhere is already
    /// stale by the time it is returned.
    /// </remarks>
    public int Count => _devices.Count;

    /// <summary>
    /// Gets the device at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the device to get.</param>
    /// <returns>The device at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside the bounds of the collection.
    /// </exception>
    /// <remarks>
    /// Read only on the thread that <c>marshal</c> runs actions on — the same rule as
    /// <see cref="Count"/> and <see cref="GetEnumerator"/>. Indexing from another thread can throw
    /// even for an index that was valid when it was chosen, because a removal can land in between.
    /// </remarks>
    public NearbyDevice this[int index] => _devices[index];

    /// <summary>
    /// Returns an enumerator that iterates through the devices currently known.
    /// </summary>
    /// <returns>An enumerator over the collection.</returns>
    /// <remarks>
    /// Enumerate only on the thread that <c>marshal</c> runs actions on, the same rule that applies
    /// to any <see cref="ObservableCollection{T}"/> bound to a user interface — mutations arrive on
    /// that thread, so enumerating elsewhere can observe a torn collection.
    /// </remarks>
    public IEnumerator<NearbyDevice> GetEnumerator() => _devices.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => ((System.Collections.IEnumerable)_devices).GetEnumerator();

    /// <summary>
    /// Stops watching the session's change stream and releases the underlying enumeration.
    /// </summary>
    /// <remarks>
    /// Idempotent — calling this more than once performs no additional work.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
    }

    /// <summary>
    /// Drains the session's change stream. Ends when the token is cancelled by
    /// <see cref="Dispose"/> — the reason this type has no unsubscribe step and cannot leak a
    /// watcher the way an event handler can.
    /// </summary>
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
        catch (OperationCanceledException)
        {
            // Disposed.
        }
        finally
        {
            await changes.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies one change. Runs inside <c>marshal</c>.
    /// </summary>
    void Apply(NearbyDeviceChange change)
    {
        var device = change.Device;
        var index = IndexOf(device);

        switch (change.Action)
        {
            case NearbyDeviceChangeAction.Added:
            case NearbyDeviceChangeAction.Updated:
                if (index >= 0)
                {
                    _devices[index] = device;
                }
                else
                {
                    _devices.Add(device);
                }

                break;

            case NearbyDeviceChangeAction.Removed:
                if (index >= 0)
                {
                    _devices.RemoveAt(index);
                }

                break;

            default:
                break;
        }
    }

    int IndexOf(NearbyDevice device) => _devices.IndexOf(device);
}