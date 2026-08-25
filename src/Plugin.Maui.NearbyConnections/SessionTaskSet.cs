namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Owns the fact "these session-started tasks are live" (contract C6). Auto-accept and the
/// disconnect watchers register here, and <c>StopAsync</c> and <c>DisposeAsync</c> both join the
/// set within a constant bound. A bare <c>_ =</c> discard of live work outside the two owning
/// types — this set and <see cref="KeyedSerialQueue"/> — is the review flag.
/// </summary>
/// <remarks>
/// Tasks self-remove on completion. <see cref="Add"/> during a join is accepted: the join loops
/// until the set is quiet or the bound elapses. Death policy — fail soft: a member's failure is
/// reported through <paramref name="onError"/> and the set shrinks; members are expected to catch
/// and log their own failures, so <paramref name="onError"/> firing means a member's catch has a
/// gap.
/// </remarks>
/// <param name="timeProvider">The clock the join bound runs on.</param>
/// <param name="onError">Receives a member task's unobserved failure.</param>
sealed class SessionTaskSet(TimeProvider timeProvider, Action<Exception> onError)
{
    readonly ConcurrentDictionary<Task, byte> _live = new();

    /// <summary>Registers a live session task. A task that already completed is not tracked.</summary>
    /// <param name="task">The running task to own.</param>
    public void Add(Task task)
    {
        if (task.IsCompleted)
        {
            Observe(task);
            return;
        }

        _live[task] = 0;
        _ = RemoveOnCompletionAsync(task);
    }

    /// <summary>
    /// Waits for every live task, looping over tasks added mid-join, until the set is quiet or
    /// the bound elapses.
    /// </summary>
    /// <param name="bound">The most time the join may take.</param>
    /// <returns>
    /// <see langword="true"/> when the set went quiet, <see langword="false"/> when the bound
    /// elapsed with tasks still live — the caller logs that.
    /// </returns>
    public async Task<bool> JoinAsync(TimeSpan bound)
    {
        using var deadline = new CancellationTokenSource(bound, timeProvider);

        while (!_live.IsEmpty)
        {
            var snapshot = _live.Keys.ToArray();

            try
            {
                await Task.WhenAll(snapshot).WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                return false;
            }
            catch
            {
                // A member completed by faulting or cancelling. Its failure is reported by
                // RemoveOnCompletionAsync; the join only cares that it is no longer live.
            }

            foreach (var task in snapshot)
            {
                if (task.IsCompleted)
                {
                    _live.TryRemove(task, out _);
                }
            }
        }

        return true;
    }

    async Task RemoveOnCompletionAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onError(ex);
        }
        finally
        {
            _live.TryRemove(task, out _);
        }
    }

    void Observe(Task task)
    {
        if (task.Exception is { } aggregate)
        {
            onError(aggregate.GetBaseException());
        }
    }
}
