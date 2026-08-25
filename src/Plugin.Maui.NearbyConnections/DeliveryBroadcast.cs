using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One broadcast delivery stream (contract C3): each enumeration first yields every item still
/// outstanding, then live arrivals, each item exactly once per enumerator. The deliverable mirror
/// of <see cref="ChangeBroadcast{T}"/> — that type carries state deltas and never replays, this
/// one carries deliverables and always replays, which is section 2's doctrine expressed as two
/// types instead of one flag.
/// </summary>
/// <typeparam name="T">The deliverable. Reference type, because the handover guard dedupes by reference.</typeparam>
/// <remarks>
/// <para>
/// <b>The handover rule.</b> At enumeration start the enumerator subscribes first, then reads the
/// outstanding set through <paramref name="snapshot"/>, yields it, then yields live items while
/// suppressing any live item reference-equal to a snapshot member. Either other order fails C3:
/// read first and an item that arrives in the window is yielded zero times, subscribe first
/// without the guard and it is yielded twice.
/// </para>
/// <para>
/// This type holds no fact state. The outstanding set stays with its owner and is read through
/// the delegate at enumeration start, and the guard set is enumerator-local, bounded by the
/// snapshot size, and dies with the enumerator — so C5's one-owner rule holds.
/// </para>
/// </remarks>
/// <param name="snapshot">Reads the outstanding set from the fact's owner.</param>
sealed class DeliveryBroadcast<T>(Func<IReadOnlyList<T>> snapshot) where T : class
{
    readonly Lock _gate = new();
    readonly List<Channel<T>> _watchers = [];
    readonly Func<IReadOnlyList<T>> _snapshot = snapshot;

    /// <summary>
    /// The broadcast stream. Every enumeration replays the outstanding set, then follows live.
    /// </summary>
    public IAsyncEnumerable<T> Stream => new DeliveryStream(this);

    /// <summary>Delivers <paramref name="item"/> to every live enumeration.</summary>
    public void Publish(T item)
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
            watcher.Writer.TryWrite(item);
        }
    }

    Channel<T> Subscribe()
    {
        // Unbounded and single-reader, like ChangeBroadcast: one enumeration drains it, and a
        // slow consumer buffers rather than blocking the publisher.
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
    /// One enumeration of <see cref="Stream"/>. A type rather than an iterator method for the same
    /// reason as <see cref="ChangeBroadcast{T}"/>: an async iterator body does not run until the
    /// first <c>MoveNextAsync</c>, and the subscription must be live — and the snapshot read —
    /// the moment <see cref="GetAsyncEnumerator"/> returns, in that order.
    /// </summary>
    sealed class DeliveryStream(DeliveryBroadcast<T> broadcast) : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            // Subscribe first, snapshot second — the handover rule. An item published between the
            // two lands in the snapshot and in the channel, and the guard suppresses the second.
            var channel = broadcast.Subscribe();
            var replay = broadcast._snapshot();

            return new Enumerator(broadcast, channel, Drain(replay, channel, cancellationToken));
        }

        static async IAsyncEnumerator<T> Drain(
            IReadOnlyList<T> replay,
            Channel<T> channel,
            CancellationToken cancellationToken)
        {
            foreach (var item in replay)
            {
                yield return item;
            }

            var guard = replay.Count == 0
                ? null
                : new HashSet<T>(replay, ReferenceEqualityComparer.Instance);

            await foreach (var item in channel.Reader
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                if (guard is not null && guard.Remove(item))
                {
                    continue;
                }

                yield return item;
            }
        }

        /// <summary>
        /// Wraps the draining iterator so that unsubscribing happens in <see cref="DisposeAsync"/>
        /// on the read path and the never-read path alike — the same discipline
        /// <see cref="ChangeBroadcast{T}"/> enforces, for the same leak.
        /// </summary>
        sealed class Enumerator(
            DeliveryBroadcast<T> broadcast,
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

                // Unsubscribe first: it completes the channel, so a Drain suspended on
                // ReadAllAsync observes end-of-stream and the inner disposal returns.
                broadcast.Unsubscribe(channel);

                await inner.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
