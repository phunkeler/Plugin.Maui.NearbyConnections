using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Fans a sequence of changes out to any number of independent consumers, each of which reads
/// every change through its own buffer.
/// </summary>
/// <remarks>
/// <para>
/// The shape behind every broadcast stream in the library — <see cref="INearbyDevices.Changes"/>,
/// <see cref="INearby.AdvertisingChanges"/>, and <see cref="INearby.DiscoveryChanges"/>. One type
/// rather than three copies because a fix applied to one copy and not its siblings is this
/// repository's dominant defect class.
/// </para>
/// <para>
/// Every enumeration of <see cref="Stream"/> gets its own unbounded channel and receives every
/// change published after it subscribed. Nothing is replayed, so read the current state first and
/// then watch — <see cref="Stream"/> subscribes eagerly, which makes that order gapless.
/// </para>
/// <para>
/// The lock guards only the watcher list, and is never held across a channel write or anything
/// awaitable, so it cannot deadlock with a consumer.
/// </para>
/// </remarks>
/// <typeparam name="T">The change being broadcast.</typeparam>
sealed class ChangeBroadcast<T>
{
    readonly Lock _gate = new();
    readonly List<Channel<T>> _watchers = [];

    /// <summary>
    /// The broadcast stream. Each enumeration subscribes independently.
    /// </summary>
    public IAsyncEnumerable<T> Stream => new ChangeStream(this);

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
        /// returns.
        /// </remarks>
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => Drain(broadcast, broadcast.Subscribe(), cancellationToken);

        static async IAsyncEnumerator<T> Drain(
            ChangeBroadcast<T> broadcast,
            Channel<T> channel,
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
                broadcast.Unsubscribe(channel);
            }
        }
    }
}
