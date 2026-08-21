namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tracks a single outgoing file transfer, routing progress through a shared
/// <see cref="IProgress{T}"/> and completing an awaitable task when the transfer
/// reaches a terminal state.
/// </summary>
sealed class OutgoingTransfer(
    IProgress<NearbyTransferProgress>? progress,
    TimeSpan inactivityTimeout,
    TimeProvider timeProvider) : IDisposable
{
    readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Lock _gate = new();
    readonly CancellationTokenSource _inactivityCts = new(inactivityTimeout, timeProvider);
    bool _disposed;

    /// <summary>
    /// Awaitable task that completes when the transfer reaches a terminal state.
    /// </summary>
    public Task Completion => _tcs.Task;

    /// <summary>
    /// Cancelled when no transfer updates have been received within the configured inactivity
    /// timeout. Reset on every call to <see cref="OnUpdate"/>. Pass
    /// <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public CancellationToken InactivityToken => _inactivityCts.Token;

    /// <summary>Called by platform code to report a progress update or terminal status.</summary>
    /// <remarks>
    /// <para>
    /// Reschedules the deadline with <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>
    /// rather than replacing the source. Both platforms read <see cref="InactivityToken"/> exactly
    /// once, into a linked source, and then await — they never re-read it. Replacing the source
    /// satisfied half the rule and broke the other half: disposing the old source killed its timer,
    /// so the captured token could never fire again, and a transfer that stalled after one update
    /// hung until the caller's own token intervened. Rescheduling keeps token identity stable, so
    /// the deadline both moves and still fires.
    /// </para>
    /// <para>
    /// A no-op once disposed. Platform callbacks arrive on their own thread and can land after the
    /// <c>finally</c> in <c>PlatformSendFileAsync</c> has already disposed this transfer. Without the
    /// guard, <c>CancelAfter</c> would throw <see cref="ObjectDisposedException"/> onto that
    /// callback thread, and progress would be reported for a transfer whose caller was already
    /// handed a timeout exception.
    /// </para>
    /// </remarks>
    public void OnUpdate(NearbyTransferProgress transferProgress)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // Timeout.InfiniteTimeSpan is a valid never-firing delay, so the infinite case needs no
            // separate branch here.
            _inactivityCts.CancelAfter(inactivityTimeout);
        }

        progress?.Report(transferProgress);

        switch (transferProgress.Status)
        {
            case NearbyTransferStatus.Success:
                _tcs.TrySetResult();
                break;
            case NearbyTransferStatus.Failure:
                _tcs.TrySetException(
                    new NearbyTransferException($"Transfer {transferProgress.PayloadId} failed."));
                break;
            case NearbyTransferStatus.Canceled:
                _tcs.TrySetCanceled();
                break;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _inactivityCts.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}