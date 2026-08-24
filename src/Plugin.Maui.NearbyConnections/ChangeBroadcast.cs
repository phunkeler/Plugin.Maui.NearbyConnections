using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed class ChangeBroadcast<T>
{
    readonly Lock _gate = new();
    readonly List<Channel<T>> _watchers = [];

    public IAsyncEnumerable<T> Stream => new ChangeStream(this);

    internal int WatcherCount
    {
        get
        {
            lock (_gate)
            {
                return _watchers.Count;
            }
        }
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
    public void Publish(T change)
    {
        Channel<T>[] watchers;

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

    Channel<T> Subscribe()
    {
        // Unbounded and single-reader: one enumeration drains it, and a slow consumer buffers
        // rather than blocking the platform callback that produced the change.
        var channel = Channel.CreateUnbounded<T>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        lock (_gate)
        {
            _watchers.Add(channel);
        }

        return channel;
    }

    void Unsubscribe(Channel<T> channel)
    {
        lock (_gate)
        {
            _watchers.Remove(channel);
        }

        channel.Writer.TryComplete();
    }

    /// <summary>
    /// One enumeration of <see cref="Stream"/>. A type rather than an iterator method so that
    /// <see cref="Stream"/> can be a property: each <c>await foreach</c> calls
    /// <see cref="GetAsyncEnumerator"/> and gets its own channel, which is what makes the stream
    /// broadcast rather than shared.
    /// </summary>
    sealed class ChangeStream(ChangeBroadcast<T> broadcast) : IAsyncEnumerable<T>
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
        /// returns. The same deferred-body rule is why the matching unsubscribe lives in
        /// <see cref="Enumerator.DisposeAsync"/> rather than in the iterator — see the remarks there.
        /// </remarks>
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var channel = broadcast.Subscribe();

            return new Enumerator(broadcast, channel, Drain(channel, cancellationToken));
        }

        static async IAsyncEnumerator<T> Drain(
            Channel<T> channel,
            CancellationToken cancellationToken)
        {
            await foreach (var change in channel.Reader
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return change;
            }
        }

        /// <summary>
        /// Wraps the draining iterator so that unsubscribing happens in <see cref="DisposeAsync"/>
        /// rather than in the iterator's own <c>finally</c>.
        /// </summary>
        /// <remarks>
        /// <b>The unsubscribe cannot live in the iterator.</b> An <c>async</c> iterator body does
        /// not begin running until the first <c>MoveNextAsync</c>, so its <c>finally</c> never runs
        /// for an enumerator that is taken and then disposed without a single read — a guard clause
        /// that returns early, or a constructor that throws after subscribing. The watcher stayed in
        /// the list forever and every later <see cref="Publish"/> wrote into a channel nothing
        /// drained. Disposing here runs on both paths, read or not, which is what makes the
        /// "ending the enumeration is the only cleanup" contract true rather than nearly true.
        /// </remarks>
        sealed class Enumerator(
            ChangeBroadcast<T> broadcast,
            Channel<T> channel,
            IAsyncEnumerator<T> inner) : IAsyncEnumerator<T>
        {
            int _disposed;

            public T Current => inner.Current;

            public ValueTask<bool> MoveNextAsync() => inner.MoveNextAsync();

            public async ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                // Unsubscribe first: it completes the channel, so a Drain suspended on ReadAllAsync
                // observes end-of-stream and the inner disposal below returns rather than blocking.
                broadcast.Unsubscribe(channel);

                await inner.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
