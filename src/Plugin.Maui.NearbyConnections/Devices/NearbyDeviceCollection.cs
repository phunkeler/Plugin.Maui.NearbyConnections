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
    /// <summary>
    /// How long a device may go unseen before it is evicted, when no interval is supplied.
    /// </summary>
    public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromSeconds(30);

    readonly ObservableCollection<NearbyDevice> _devices = [];
    readonly CancellationTokenSource _cts = new();
    readonly Action<Action> _marshal;
    readonly INearby _nearby;
    readonly TimeSpan? _staleAfter;
    readonly TimeProvider _timeProvider;

    /// <summary>
    /// Last-seen timestamps, used only when <c>staleAfter</c> is set. Keyed by device id because a
    /// device snapshot is a value and cannot carry mutable bookkeeping.
    /// </summary>
    /// <remarks>
    /// Touched only inside <c>marshal</c>, on the same thread as <see cref="_devices"/>, so it needs
    /// no lock of its own.
    /// </remarks>
    readonly Dictionary<string, DateTimeOffset> _lastSeen = new(StringComparer.Ordinal);

    int _disposeGuard;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyDeviceCollection"/> class and begins
    /// watching for device changes, evicting devices unseen for <see cref="DefaultStaleAfter"/>.
    /// </summary>
    /// <param name="nearby">The session to watch.</param>
    /// <param name="marshal">
    /// Runs an action where collection mutations are safe — in .NET MAUI,
    /// <see cref="IDispatcher.Dispatch(Action)"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nearby"/> or <paramref name="marshal"/> is <see langword="null"/>.
    /// </exception>
    public NearbyDeviceCollection(INearby nearby, Action<Action> marshal)
        : this(nearby, marshal, DefaultStaleAfter)
    {
    }

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
    /// <param name="staleAfter">
    /// How long a device may go unseen before it is removed, or <see langword="null"/> to disable
    /// eviction and leave removal entirely to platform "lost" signals.
    /// <para>
    /// Neither platform reliably reports every departure — a device carried out of range may simply
    /// stop being seen. A connected device is never evicted, however long it has been quiet: it is
    /// demonstrably still there.
    /// </para>
    /// </param>
    /// <param name="timeProvider">
    /// Time source, for deterministic tests. Defaults to the system clock.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nearby"/> or <paramref name="marshal"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="staleAfter"/> is negative or zero.
    /// </exception>
    /// <remarks>
    /// <paramref name="staleAfter"/> cannot carry <see cref="DefaultStaleAfter"/> as a C# default —
    /// a <see cref="TimeSpan"/> is not a compile-time constant — so the two-overload form is what
    /// distinguishes "unspecified" (use the default) from an explicit <see langword="null"/>
    /// (disable eviction).
    /// </remarks>
    public NearbyDeviceCollection(
        INearby nearby,
        Action<Action> marshal,
        TimeSpan? staleAfter,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(marshal);

        if (staleAfter is { } interval)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero, nameof(staleAfter));
        }

        _nearby = nearby;
        _marshal = marshal;
        _staleAfter = staleAfter;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Subscribe before seeding, so a change arriving between the two is buffered by the
        // enumeration rather than lost. GetAsyncEnumerator subscribes eagerly for exactly this
        // reason — see NearbyDeviceRegistry.ChangeStream. Apply then reconciles by id, so a device
        // present in both the seed and an early change is updated, not duplicated.
        var changes = _nearby.Devices.Changes.GetAsyncEnumerator(_cts.Token);

        // Through marshal like every other mutation: this collection may be constructed off the UI
        // thread, and WatchAsync can already be marshalling additions onto it.
        _marshal(() =>
        {
            foreach (var device in _nearby.Devices)
            {
                _devices.Add(device);
                _lastSeen[device.Id] = _timeProvider.GetUtcNow();
            }
        });

        _ = WatchAsync(changes);

        if (_staleAfter is not null)
        {
            _ = SweepAsync(_cts.Token);
        }
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
    public int Count => _devices.Count;

    /// <summary>
    /// Gets the device at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the device to get.</param>
    /// <returns>The device at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside the bounds of the collection.
    /// </exception>
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

        // Cancel only. WatchAsync and SweepAsync resume on other threads and still read this
        // token; disposing here would make that read throw ObjectDisposedException, which escapes
        // their `catch (OperationCanceledException)` and faults an unobserved task. Cancellation
        // alone ends both loops, and the source is collectable once they do.
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
                _lastSeen[device.Id] = _timeProvider.GetUtcNow();

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
                _lastSeen.Remove(device.Id);

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
    /// Removes devices not seen within <c>staleAfter</c>. Runs until disposal.
    /// </summary>
    async Task SweepAsync(CancellationToken cancellationToken)
    {
        // Half the stale window: frequent enough that a departed device disappears promptly, cheap
        // enough to be invisible.
        var interval = TimeSpan.FromTicks(_staleAfter!.Value.Ticks / 2);

        using var timer = new PeriodicTimer(interval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                _marshal(EvictStale);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    /// <summary>
    /// Drops every device whose last sighting is older than <c>staleAfter</c>. Runs inside
    /// <c>marshal</c>.
    /// </summary>
    void EvictStale()
    {
        var cutoff = _timeProvider.GetUtcNow() - _staleAfter!.Value;

        for (var i = _devices.Count - 1; i >= 0; i--)
        {
            var device = _devices[i];

            // A device mid-handshake or connected is never stale: it is demonstrably still there,
            // whatever discovery has stopped reporting.
            if (device.Status is not NearbyDeviceStatus.Visible)
            {
                continue;
            }

            if (_lastSeen.TryGetValue(device.Id, out var seen) && seen >= cutoff)
            {
                continue;
            }

            _lastSeen.Remove(device.Id);
            _devices.RemoveAt(i);
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
