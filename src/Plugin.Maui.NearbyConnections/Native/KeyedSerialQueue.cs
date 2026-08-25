namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Runs queued work one item at a time per key, and lets a caller wait for the queued work to
/// finish before it frees what that work reads.
/// </summary>
/// <remarks>
/// <para>
/// This is the mechanism behind "drain, then release" for per-peer work. The key is a peer id.
/// Items for one peer run in the order they arrive. Items for different peers run independently.
/// </para>
/// <para>
/// <b>What belongs on this queue.</b> Enqueue work that is finite, keyed by peer, and started from
/// a platform callback that cannot await it. Inbound payload completion on Android is the primary
/// case.
/// </para>
/// <para>
/// <b>What does not belong on it.</b> Three exclusions, each for a different reason:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Waits, as opposed to work.</b> A timer, a disconnect watcher, or an await on a
/// <see cref="TaskCompletionSource"/> completes only when something outside the queue happens. Such
/// an item blocks every later item for that peer until it completes.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Anything that drains this queue.</b> A release or a disposal waits for the queue. If a caller
/// enqueues that wait, the item waits for itself and never completes.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Work a caller already awaits.</b> A public asynchronous operation has an owner and a deadline.
/// See the termination guarantees in <c>AGENTS.md</c>.
/// </description>
/// </item>
/// </list>
/// <para>
/// See <c>docs/ARCHITECTURE.md</c> section 3 (contracts C6 and C7) for how this queue relates to
/// channel delivery, and for the limit on work that arrives after a drain starts.
/// </para>
/// </remarks>
/// <param name="onError">
/// Receives the key and the exception when queued work throws. The queue reports every failure here
/// and never faults the key, so one failed item cannot stop the items behind it.
/// </param>
sealed class KeyedSerialQueue(Action<string, Exception> onError)
{
    readonly Lock _gate = new();
    readonly Dictionary<string, Task> _tails = new(StringComparer.Ordinal);

    /// <summary>
    /// Queues <paramref name="work"/> behind the work already queued for <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// The work always starts on a thread pool thread. It never runs on the calling thread, so a
    /// platform callback that enqueues work returns without waiting for it. This matters on
    /// Android, where the callback thread may be the main thread and the work reads files.
    /// </remarks>
    /// <param name="key">The peer id the work belongs to.</param>
    /// <param name="work">The work to run. Exceptions from it go to the error handler.</param>
    /// <returns>
    /// A task that completes when this item completes, which is after every item queued before it.
    /// The task never faults.
    /// </returns>
    public Task Enqueue(string key, Func<Task> work)
    {
        lock (_gate)
        {
            var previous = _tails.GetValueOrDefault(key, Task.CompletedTask);

            // Task.Run is what keeps the work off the caller's thread. Without it the first item
            // for an idle key runs inline, inside this lock, on the platform's callback thread.
            var queued = Task.Run(() => RunAsync(key, previous, work));

            _tails[key] = queued;

            // Prune from a continuation, not from inside RunAsync: a task is not yet marked
            // completed while its own body runs, so self-removal there never fires.
            _ = queued.ContinueWith(
                completed => Prune(key, completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return queued;
        }
    }

    /// <summary>
    /// Waits for the work queued for <paramref name="key"/> when this call starts.
    /// </summary>
    /// <param name="key">The peer id to wait for.</param>
    /// <param name="bound">
    /// How long to wait. The wait is always bounded, so a stuck item cannot turn a release or a
    /// disposal into a hang.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the work finished. <see langword="false"/> when
    /// <paramref name="bound"/> elapsed first. The caller logs that result.
    /// </returns>
    public Task<bool> DrainAsync(string key, TimeSpan bound)
    {
        Task? tail;

        lock (_gate)
        {
            tail = _tails.GetValueOrDefault(key);
        }

        return tail is null ? Task.FromResult(true) : WaitAsync(tail, bound);
    }

    /// <summary>
    /// Waits for the work queued for every key when this call starts. Use this at disposal.
    /// </summary>
    /// <param name="bound">
    /// How long to wait for all of the work together.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the work finished. <see langword="false"/> when
    /// <paramref name="bound"/> elapsed first.
    /// </returns>
    public Task<bool> DrainAllAsync(TimeSpan bound)
    {
        Task[] tails;

        lock (_gate)
        {
            tails = [.. _tails.Values];
        }

        return tails.Length == 0 ? Task.FromResult(true) : WaitAsync(Task.WhenAll(tails), bound);
    }

    /// <summary>
    /// The number of keys that still hold work, reported when a drain times out. This is a snapshot:
    /// a key prunes itself asynchronously, so do not branch on the value.
    /// </summary>
    public int KeyCount
    {
        get
        {
            lock (_gate)
            {
                return _tails.Count;
            }
        }
    }

    async Task RunAsync(string key, Task previous, Func<Task> work)
    {
        // The predecessor never faults, because this method catches everything.
        await previous.ConfigureAwait(false);

        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onError(key, ex);
        }
    }

    static async Task<bool> WaitAsync(Task tail, TimeSpan bound)
    {
        try
        {
            await tail.WaitAsync(bound).ConfigureAwait(false);

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    // Drops a key once its last item finishes, so a long-lived session does not hold one completed
    // task per peer forever. Only the tail prunes: a later item that replaced the entry owns it, so
    // this call sees a different task and leaves the entry alone.
    void Prune(string key, Task completed)
    {
        lock (_gate)
        {
            if (_tails.TryGetValue(key, out var current) && ReferenceEquals(current, completed))
            {
                _tails.Remove(key);
            }
        }
    }
}
