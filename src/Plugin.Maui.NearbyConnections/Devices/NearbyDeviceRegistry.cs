using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The session's device set: the authoritative store behind <see cref="INearby.Devices"/>, and the
/// broadcast source behind <see cref="INearbyDevices.Changes"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No dispatcher.</b> Reads take an immutable snapshot and writes are serialised by a lock, so
/// every member is callable from any thread. That is the whole point of this type: platform
/// callbacks no longer have to hop to the UI thread to record what happened, and the two off-thread
/// reads that <c>DisconnectAsync</c> and <c>StopAsync</c> used to perform are no longer races.
/// </para>
/// <para>
/// The lock covers the dictionary and the snapshot together. It is held only for the duration of a
/// dictionary write and a snapshot rebuild — never across a channel write, and never across
/// anything awaitable — so it cannot deadlock with a consumer.
/// </para>
/// </remarks>
sealed class NearbyDeviceRegistry : INearbyDevices
{
    readonly Lock _gate = new();
    readonly Dictionary<string, NearbyDevice> _devices = new(StringComparer.Ordinal);
    readonly List<Channel<NearbyDeviceChange>> _watchers = [];

    /// <summary>
    /// Devices carried over from the previous discovery generation that the current pass has not
    /// re-reported yet. Guarded by <see cref="_gate"/> like every other mutable field here.
    /// </summary>
    readonly HashSet<string> _unconfirmed = new(StringComparer.Ordinal);

    /// <summary>
    /// The current set, rebuilt on every mutation so a reader never touches the dictionary. Readers
    /// take this reference and enumerate it outside the lock; a concurrent mutation replaces the
    /// reference rather than modifying the array being read.
    /// </summary>
    volatile NearbyDevice[] _snapshot = [];

    /// <inheritdoc/>
    public int Count => _snapshot.Length;

    /// <inheritdoc/>
    public NearbyDevice this[int index] => _snapshot[index];

    /// <inheritdoc/>
    public IAsyncEnumerable<NearbyDeviceChange> Changes => new ChangeStream(this);

    /// <inheritdoc/>
    public IEnumerator<NearbyDevice> GetEnumerator()
        => ((IEnumerable<NearbyDevice>)_snapshot).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => _snapshot.GetEnumerator();

    /// <summary>
    /// Looks up a device by id.
    /// </summary>
    public bool TryGet(string id, [NotNullWhen(true)] out NearbyDevice? device)
    {
        lock (_gate)
        {
            return _devices.TryGetValue(id, out device);
        }
    }

    /// <summary>
    /// Adds a device if it is not already present, and publishes
    /// <see cref="NearbyDeviceChangeAction.Added"/> if it was not.
    /// </summary>
    /// <returns>The device now in the set — the existing one if there was one.</returns>
    /// <remarks>
    /// <para>
    /// Keeping the incumbent rather than overwriting it is what stops a rediscovery from resetting
    /// a device's status: the platform hands over a freshly built <see cref="NearbyDeviceStatus.Visible"/>
    /// snapshot every time it sees a device, and a device found again while connected is still
    /// connected.
    /// </para>
    /// <para>
    /// That protects the <em>add</em> only. A caller that follows this with an explicit transition
    /// — as the connect and accept paths do — is stating the new status deliberately and overwrites
    /// it, which is the intended behaviour.
    /// </para>
    /// </remarks>
    public NearbyDevice AddIfAbsent(NearbyDevice device)
    {
        NearbyDeviceChange change;

        lock (_gate)
        {
            // Confirms the device for this discovery generation whether or not it is new: a
            // rediscovery is exactly the evidence EvictUnconfirmed is waiting for, and it arrives
            // through this early return rather than through a published change.
            _unconfirmed.Remove(device.Id);

            if (_devices.TryGetValue(device.Id, out var existing))
            {
                return existing;
            }

            _devices[device.Id] = device;
            Rebuild();
            change = new NearbyDeviceChange(NearbyDeviceChangeAction.Added, device);
        }

        Publish(change);
        return device;
    }

    /// <summary>
    /// Applies <paramref name="update"/> to the stored device and publishes
    /// <see cref="NearbyDeviceChangeAction.Updated"/> if the result differs.
    /// </summary>
    /// <param name="id">The device to update.</param>
    /// <param name="update">
    /// Produces the new snapshot from the current one. Runs under the lock, so it must be pure and
    /// must not call back into the session.
    /// </param>
    /// <returns>
    /// The updated device, or <see langword="null"/> if no device with that id is present.
    /// </returns>
    /// <remarks>
    /// Read-modify-write under one lock, rather than a caller reading and then writing: two platform
    /// callbacks racing on the same device would otherwise interleave and lose one of the writes.
    /// </remarks>
    public NearbyDevice? Update(string id, Func<NearbyDevice, NearbyDevice> update)
    {
        NearbyDeviceChange change;

        lock (_gate)
        {
            if (!_devices.TryGetValue(id, out var current))
            {
                return null;
            }

            // Any transition is proof of life — a device that connects or receives a request during
            // a generation must not then be evicted by it.
            _unconfirmed.Remove(id);

            var updated = update(current);

            // Reference equality: NearbyDevice equality is Id-only, so `==` would call every update
            // a no-op. `with` returns a new instance whenever anything changed, and the same
            // instance is only returned when the update chose not to change anything.
            if (ReferenceEquals(updated, current))
            {
                return current;
            }

            _devices[id] = updated;
            Rebuild();
            change = new NearbyDeviceChange(NearbyDeviceChangeAction.Updated, updated);
        }

        Publish(change);
        return change.Device;
    }

    /// <summary>
    /// Removes a device and publishes <see cref="NearbyDeviceChangeAction.Removed"/> if it was
    /// present.
    /// </summary>
    public bool Remove(string id)
    {
        NearbyDeviceChange change;

        lock (_gate)
        {
            if (!_devices.Remove(id, out var removed))
            {
                return false;
            }

            Rebuild();
            change = new NearbyDeviceChange(NearbyDeviceChangeAction.Removed, removed);
        }

        Publish(change);
        return true;
    }

    /// <summary>
    /// Removes every device matching <paramref name="predicate"/>, publishing one
    /// <see cref="NearbyDeviceChangeAction.Removed"/> per device.
    /// </summary>
    /// <remarks>
    /// Per-device changes rather than a single clear notification: a consumer applying deltas needs
    /// to know which devices went away, and <see cref="NearbyDeviceChangeAction"/> has no bulk case
    /// precisely so that every change names its device.
    /// </remarks>
    public void RemoveWhere(Func<NearbyDevice, bool> predicate)
    {
        List<NearbyDeviceChange> changes = [];

        lock (_gate)
        {
            // The dictionary, not _snapshot: the two are only in step because every mutation
            // rebuilds, and reading the derived copy here would make any future path that forgets
            // to rebuild silently skip devices. Copied because the loop removes as it goes.
            foreach (var device in _devices.Values.ToArray())
            {
                if (predicate(device) && _devices.Remove(device.Id))
                {
                    changes.Add(new NearbyDeviceChange(NearbyDeviceChangeAction.Removed, device));
                }
            }

            if (changes.Count == 0)
            {
                return;
            }

            Rebuild();
        }

        foreach (var change in changes)
        {
            Publish(change);
        }
    }

    /// <summary>
    /// Removes every device.
    /// </summary>
    public void Clear() => RemoveWhere(static _ => true);

    /// <summary>
    /// Marks every device currently known as belonging to the previous discovery generation, so
    /// that <see cref="EvictUnconfirmed"/> can tell which ones a fresh discovery pass re-reported.
    /// </summary>
    /// <remarks>
    /// Publishes nothing: this is bookkeeping, not a state change a consumer should react to.
    /// </remarks>
    public void BeginGeneration()
    {
        lock (_gate)
        {
            _unconfirmed.Clear();

            foreach (var device in _devices.Values)
            {
                // Only visible devices are candidates. A connected device is demonstrably present
                // whatever discovery reports, and one mid-handshake is being acted on right now.
                if (device.Status is NearbyDeviceStatus.Visible)
                {
                    _unconfirmed.Add(device.Id);
                }
            }
        }
    }

    /// <summary>
    /// Removes every device that was present when <see cref="BeginGeneration"/> was called and has
    /// not been seen since, publishing one <see cref="NearbyDeviceChangeAction.Removed"/> each.
    /// </summary>
    /// <remarks>
    /// A device survives by being re-reported: <see cref="AddIfAbsent"/> confirms it, as does any
    /// status transition through <see cref="Update"/>. This is the only sound basis for eviction
    /// available — both platforms report discovery on an edge (once, when a device appears) rather
    /// than on a level, so elapsed silence carries no information about whether a device is still
    /// there. A completed discovery pass does.
    /// </remarks>
    public void EvictUnconfirmed() => RemoveWhere(device => _unconfirmed.Contains(device.Id));

    /// <summary>
    /// Rebuilds the read snapshot. Caller must hold <see cref="_gate"/>.
    /// </summary>
    void Rebuild()
    {
        var snapshot = new NearbyDevice[_devices.Count];
        _devices.Values.CopyTo(snapshot, 0);
        _snapshot = snapshot;
    }

    /// <summary>
    /// Fans a change out to every watcher. Called outside <see cref="_gate"/>.
    /// </summary>
    /// <remarks>
    /// The watcher list is copied under the lock and written to outside it: a channel write is
    /// cheap but is not this type's code, and holding a lock across foreign code is how deadlocks
    /// are built. Each watcher's channel is unbounded, so <c>TryWrite</c> only fails on a channel
    /// that is already completed — a watcher that has been disposed and not yet unregistered — and
    /// dropping the change for it is correct.
    /// </remarks>
    void Publish(NearbyDeviceChange change)
    {
        Channel<NearbyDeviceChange>[] watchers;

        lock (_gate)
        {
            if (_watchers.Count == 0)
            {
                return;
            }

            watchers = [.. _watchers];
        }

        foreach (var watcher in watchers)
        {
            watcher.Writer.TryWrite(change);
        }
    }

    Channel<NearbyDeviceChange> Subscribe()
    {
        // Unbounded and single-reader: one enumeration drains it, and a slow consumer buffers
        // rather than blocking the platform callback that produced the change.
        var channel = Channel.CreateUnbounded<NearbyDeviceChange>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        lock (_gate)
        {
            _watchers.Add(channel);
        }

        return channel;
    }

    void Unsubscribe(Channel<NearbyDeviceChange> channel)
    {
        lock (_gate)
        {
            _watchers.Remove(channel);
        }

        channel.Writer.TryComplete();
    }

    /// <summary>
    /// One enumeration of <see cref="Changes"/>. A type rather than an iterator method on the
    /// registry so that <see cref="Changes"/> can be a property: each <c>await foreach</c> calls
    /// <see cref="GetAsyncEnumerator"/> and gets its own channel, which is what makes the stream
    /// broadcast rather than shared.
    /// </summary>
    sealed class ChangeStream(NearbyDeviceRegistry registry) : IAsyncEnumerable<NearbyDeviceChange>
    {
        /// <summary>
        /// Subscribes, then returns an enumerator that drains the resulting channel.
        /// </summary>
        /// <remarks>
        /// <b>Not an iterator, deliberately.</b> An <c>async</c> iterator body does not begin
        /// running until the first <c>MoveNextAsync</c>, so subscribing inside one would silently
        /// drop every change published between <c>GetAsyncEnumerator</c> and that first call —
        /// exactly the window a consumer uses to read the current state before watching for what
        /// happens next. Subscribing in this plain method makes the watcher live the moment it
        /// returns.
        /// </remarks>
        public IAsyncEnumerator<NearbyDeviceChange> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
            => Drain(registry, registry.Subscribe(), cancellationToken);

        static async IAsyncEnumerator<NearbyDeviceChange> Drain(
            NearbyDeviceRegistry registry,
            Channel<NearbyDeviceChange> channel,
            CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var change in channel.Reader
                    .ReadAllAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    yield return change;
                }
            }
            finally
            {
                registry.Unsubscribe(channel);
            }
        }
    }
}
