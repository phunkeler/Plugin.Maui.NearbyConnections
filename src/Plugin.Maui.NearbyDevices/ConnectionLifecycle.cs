using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Shared lifecycle, connection-monitoring, and event-fan-out machinery used by both
/// <see cref="NearbyAdvertiser"/> and <see cref="NearbyDiscoverer"/>. Not part of the public API.
/// </summary>
/// <typeparam name="TPending">The pending-item type tracked before a connection exists
/// (<see cref="NearbyConnectionRequest"/> for the advertiser, <see cref="NearbyDevice"/> for the
/// discoverer).</typeparam>
/// <typeparam name="TEvent">The event union type published to subscribers
/// (<see cref="AdvertiserEvent"/> or <see cref="DiscovererEvent"/>).</typeparam>
sealed class ConnectionLifecycle<TPending, TEvent> where TPending : notnull
{
    readonly ChannelBroadcaster<TEvent> _broadcaster = new();
    CancellationTokenSource? _cts;
    Task? _executeTask;

    /// <summary>
    /// The lock guarding <see cref="PendingSnapshot"/> and <see cref="ActiveSnapshot"/>. Owners
    /// must hold this lock for any mutation of either snapshot, matching the lock-acquisition
    /// granularity of the code this type was extracted from.
    /// </summary>
    internal Lock StateLock { get; } = new();

    /// <summary>Pending items awaiting a connection outcome. Mutate only under <see cref="StateLock"/>.</summary>
    internal List<TPending> PendingSnapshot { get; } = [];

    /// <summary>Currently active connections. Mutate only under <see cref="StateLock"/>.</summary>
    internal List<NearbyConnection> ActiveSnapshot { get; } = [];

    /// <summary>The cancellation token for the current in-flight <see cref="_executeTask"/>, or a
    /// non-cancelable token if no operation is running.</summary>
    internal CancellationToken CurrentServiceToken
    {
        get
        {
            lock (StateLock)
            {
                return _cts?.Token ?? CancellationToken.None;
            }
        }
    }

    /// <summary>
    /// Cancels and awaits any previous in-flight operation, then starts a new one via
    /// <paramref name="executeTaskFactory"/>, publishing an expiry event for every pending item
    /// left over from the previous run.
    /// </summary>
    /// <param name="executeTaskFactory">Produces the <see cref="Task"/> to track, given the new
    /// operation's <see cref="CancellationToken"/>.</param>
    /// <param name="onPendingExpired">Constructs the expiry event published for each pending item.</param>
    /// <param name="setRunningFlag">Sets the owner's running flag once the new operation has started.</param>
    internal async Task StartAsync(
        Func<CancellationToken, Task> executeTaskFactory,
        Func<TPending, TEvent> onPendingExpired,
        Action<bool> setRunningFlag)
    {
        Task? previousExecuteTask;
        lock (StateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            previousExecuteTask = _executeTask;
            foreach (var item in PendingSnapshot)
            {
                _broadcaster.Publish(onPendingExpired(item));
            }
            PendingSnapshot.Clear();
        }

        // Wait for the previous execution to fully unwind — including the platform's stop
        // teardown, which runs synchronously inside its finally block — before starting a new
        // one. Without this, a rapid stop/restart could have two operations alive at once, both
        // touching the same native advertiser/browser field.
        if (previousExecuteTask is not null)
        {
            try
            {
                await previousExecuteTask;
            }
            catch (OperationCanceledException)
            {
                // Normal exit when the previous StartAsync/StopAsync cancelled the token.
            }
        }

        lock (StateLock)
        {
            var cts = new CancellationTokenSource();
            _cts = cts;
            _executeTask = executeTaskFactory(cts.Token);
        }

        setRunningFlag(true);
    }

    /// <summary>
    /// Cancels the in-flight operation, publishing an expiry event for every pending item, and
    /// awaits its teardown.
    /// </summary>
    /// <param name="onPendingExpired">Constructs the expiry event published for each pending item.</param>
    /// <param name="setRunningFlag">Clears the owner's running flag before teardown begins.</param>
    internal async Task StopAsync(Func<TPending, TEvent> onPendingExpired, Action<bool> setRunningFlag)
    {
        setRunningFlag(false);
        Task? executeTask;
        lock (StateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            executeTask = _executeTask;
            foreach (var item in PendingSnapshot)
            {
                _broadcaster.Publish(onPendingExpired(item));
            }
            PendingSnapshot.Clear();
        }

        if (executeTask is not null)
        {
            try
            {
                await executeTask;
            }
            catch (OperationCanceledException)
            {
                // Normal exit when the cancellation above stops the operation.
            }
        }
    }

    // A synchronous Dispose() cannot await the execute task's teardown without either blocking
    // the calling thread (violating async-all-the-way) or risking deadlock. This is a
    // pre-existing, accepted limitation of implementing both IDisposable and IAsyncDisposable
    // side by side — callers who need to know the platform session has fully stopped before
    // returning must use DisposeAsync() instead.
    internal void Dispose()
    {
        lock (StateLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _broadcaster.Complete();
        }
    }

    internal async ValueTask DisposeAsync()
    {
        Task? executeTask;
        NearbyConnection[] connections;
        lock (StateLock)
        {
            connections = [.. ActiveSnapshot];
            ActiveSnapshot.Clear();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            executeTask = _executeTask;
        }

        if (executeTask is not null)
        {
            try
            {
                await executeTask;
            }
            catch (OperationCanceledException)
            {
                // Normal exit when the cancellation above stops the operation.
            }
        }

        foreach (var conn in connections)
        {
            await conn.DisposeAsync();
        }

        lock (StateLock)
        {
            _broadcaster.Complete();
        }
    }

    /// <summary>
    /// Publishes <paramref name="ev"/> to all current subscribers. Caller must already hold
    /// <see cref="StateLock"/> — this does not acquire it, so it can be composed with snapshot
    /// mutations under one atomic lock acquisition (e.g. add-to-pending-and-publish).
    /// </summary>
    internal void Publish(TEvent ev) => _broadcaster.Publish(ev);

    /// <summary>Completes all subscriber channels with <paramref name="fault"/>, ending the service's event stream.</summary>
    internal void Fault(Exception fault)
    {
        lock (StateLock)
        {
            _broadcaster.Complete(fault);
        }
    }

    /// <summary>
    /// Watches <paramref name="conn"/> for disconnect and publishes a dropped event, removing it
    /// from <see cref="ActiveSnapshot"/> regardless of whether the disconnect was real or
    /// <paramref name="serviceToken"/> was cancelled by <see cref="StopAsync"/>.
    /// </summary>
    internal async Task MonitorConnectionAsync(
        NearbyConnection conn,
        Func<NearbyConnection, TEvent> onDropped,
        Action<string, string?> logDropped,
        CancellationToken serviceToken)
    {
        try
        {
            await conn.Disconnected.WaitAsync(serviceToken);
            logDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
            lock (StateLock)
            {
                ActiveSnapshot.Remove(conn);
                _broadcaster.Publish(onDropped(conn));
            }
        }
        catch (OperationCanceledException)
        {
            // serviceToken is cancelled by StopAsync(), not by the connection itself dropping -
            // but from a subscriber's perspective (e.g. ConnectionsPageViewModel.ConnectedDevices,
            // built by replaying EventsAsync's snapshot + live events) this connection is gone
            // either way. Removing it from _activeSnapshot without publishing the dropped event
            // meant a subscriber that already added this connection before the stop never learned
            // it should remove it - it would silently stay in a UI collection forever, even though
            // it no longer appears in any future EventsAsync snapshot replay. Confirmed via
            // observed "5+ connections" accumulating in the Connections page across repeated
            // advertise-or-discover/stop cycles.
            logDropped(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName);
            lock (StateLock)
            {
                ActiveSnapshot.Remove(conn);
                _broadcaster.Publish(onDropped(conn));
            }
        }
    }

    /// <summary>Forwards payloads received on <paramref name="conn"/> to subscribers until it disconnects.</summary>
    internal async Task ForwardPayloadsAsync(
        NearbyConnection conn,
        Func<NearbyConnection, NearbyPayload, TEvent> onPayload,
        Action<string, string?, Exception> logError,
        CancellationToken ct)
    {
        try
        {
            await foreach (var payload in conn.ReceiveAsync(ct))
            {
                lock (StateLock)
                {
                    _broadcaster.Publish(onPayload(conn, payload));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Service stopped; normal exit.
        }
        catch (Exception ex)
        {
            logError(conn.RemoteDevice.Id, conn.RemoteDevice.DisplayName, ex);
        }
    }

    /// <summary>
    /// Yields the current-state snapshot (built via <paramref name="buildSnapshot"/>) followed by
    /// <paramref name="synchronized"/>, then live events until <paramref name="cancellationToken"/>
    /// is cancelled.
    /// </summary>
    /// <param name="buildSnapshot">
    /// Projects <see cref="PendingSnapshot"/>/<see cref="ActiveSnapshot"/> into events. Evaluated
    /// and eagerly materialized to a list while <see cref="StateLock"/> is still held, so it is
    /// safe to return a lazy LINQ query here — the caller does not need to call
    /// <c>.ToList()</c> itself.
    /// </param>
    /// <param name="synchronized">Constructs the sentinel event marking the snapshot/live boundary.</param>
    /// <param name="cancellationToken">Cancelling this ends enumeration.</param>
    internal async IAsyncEnumerable<TEvent> EventsAsync(
        Func<IEnumerable<TEvent>> buildSnapshot,
        Func<TEvent> synchronized,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<TEvent> sub;
        List<TEvent> snapshot;

        lock (StateLock)
        {
            // Materialize inside the lock: buildSnapshot() may return a lazy query closed over
            // the live, mutable PendingSnapshot/ActiveSnapshot lists. Enumerating it after the
            // lock releases would race any concurrent mutation (Accept/Reject/RunLoopAsync) and
            // throw InvalidOperationException ("Collection was modified") mid-enumeration.
            snapshot = [.. buildSnapshot()];
            sub = _broadcaster.Subscribe();
        }

        try
        {
            foreach (var ev in snapshot)
            {
                yield return ev;
            }

            yield return synchronized();

            await foreach (var ev in sub.Reader.ReadAllAsync(cancellationToken))
            {
                yield return ev;
            }
        }
        finally
        {
            lock (StateLock) { _broadcaster.Unsubscribe(sub); }
        }
    }
}
