using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A bindable, live collection of nearby devices, kept up to date by consuming
/// <see cref="INearbyDevices.Changes"/> and applying each change on a caller-supplied thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is optional.</b> <see cref="INearby"/> itself has no thread affinity: it hands out
/// immutable snapshots and a change stream that can be consumed from anywhere. This class exists
/// only for consumers who want a collection they can bind to XAML, and it is the single place in
/// the library that knows a UI thread exists.
/// </para>
/// <para>
/// Construct one per view that needs it and dispose it when the view goes away. Disposal cancels
/// the enumeration; there is no event to unsubscribe from and therefore no subscription to leak.
/// </para>
/// <para>
/// The collection is read-only to consumers: it reflects what the session reports and cannot be
/// added to or removed from directly. It raises
/// <see cref="INotifyCollectionChanged.CollectionChanged"/>, so it can be bound to an
/// <c>ItemsSource</c> directly.
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
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "This is a collection: it is enumerable and raises CollectionChanged. CA1711 " +
        "wants ICollection specifically, but the mutating half of that interface is deliberately " +
        "not offered — the session is the only writer. Every *Collection type across MAUI, " +
        "Avalonia and Newtonsoft.Json is enumerable; several (Avalonia's FamilyNameCollection, " +
        "EmbeddedFontCollection, GestureRecognizerCollection) are likewise not ICollection.")]
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
    /// A callback rather than a dependency on <see cref="IDispatcher"/> so this type stays
    /// platform-neutral: it compiles and is testable on the <c>net10.0</c> target, and all three
    /// public API baselines stay identical.
    /// </para>
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nearby"/> or <paramref name="marshal"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// A device is removed when the platform reports it lost, and not before. Neither platform
    /// reliably reports every departure, so a device carried out of range can linger until
    /// discovery restarts — the alternative, evicting on a timer, would need a periodic "still
    /// here" signal that <see cref="INearbyDevices.Changes"/> does not carry, and would delete
    /// devices that are still present.
    /// </remarks>
    public NearbyDeviceCollection(INearby nearby, Action<Action> marshal)
    {
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(marshal);

        _nearby = nearby;
        _marshal = marshal;

        // Subscribe before seeding, so a change arriving between the two is buffered by the
        // enumeration rather than lost. GetAsyncEnumerator subscribes eagerly for exactly this
        // reason — see NearbyDeviceRegistry.ChangeStream. Apply then reconciles by id, so a device
        // present in both the seed and an early change is updated, not duplicated.
        var changes = _nearby.Devices.Changes.GetAsyncEnumerator(_cts.Token);

        // Through marshal like every other mutation: this collection may be constructed off the UI
        // thread, and WatchAsync can already be marshalling additions onto it. The seed is queued
        // before WatchAsync starts, so an ordered marshal (a dispatcher queue, or an inline call)
        // runs it before any change it might overlap with.
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
    /// Raised inside the <c>marshal</c> callback, so a handler — including a XAML binding — runs on
    /// whatever thread the caller nominated as safe.
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
    /// Read only on the thread that <c>marshal</c> runs actions on. Devices are added and removed
    /// on that thread, so a count read anywhere else is stale the moment it is returned.
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
    /// Read only on the thread that <c>marshal</c> runs actions on — the same rule that applies to
    /// <see cref="Count"/> and <see cref="GetEnumerator"/>. Indexing from another thread can throw
    /// even for an index that was valid when it was chosen, because a removal may land in between.
    /// </remarks>
    public NearbyDevice this[int index] => _devices[index];

    /// <summary>
    /// Returns an enumerator that iterates through the devices currently known.
    /// </summary>
    /// <returns>An enumerator over the collection.</returns>
    /// <remarks>
    /// Enumerate only on the thread that <c>marshal</c> runs actions on — the same rule that
    /// applies to any <see cref="ObservableCollection{T}"/> bound to a user interface. Mutations
    /// arrive on that thread, so enumerating anywhere else can observe a torn collection.
    /// </remarks>
    public IEnumerator<NearbyDevice> GetEnumerator() => _devices.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => ((System.Collections.IEnumerable)_devices).GetEnumerator();

    /// <summary>
    /// Stops watching and releases the enumeration.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        // Cancel only. WatchAsync resumes on another thread and still reads this token; disposing
        // here would make that read throw ObjectDisposedException, which escapes its
        // `catch (OperationCanceledException)` and faults an unobserved task. Cancellation alone
        // ends the loop, and the source is collectable once it does.
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
                // A device is a value, so an update is a replacement, not a property write. The
                // indexer assignment raises NotifyCollectionChangedAction.Replace, which a bound
                // row observes as an in-place update.
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

    /// <summary>
    /// Finds a device's position by id. Linear because the collection is a visible device list —
    /// tens of entries, not thousands.
    /// </summary>
    // NearbyDevice equality is Id-only (see its Equals override), so the collection's own IndexOf
    // already matches on identity regardless of how the device's status has since changed.
    int IndexOf(NearbyDevice device) => _devices.IndexOf(device);
}
