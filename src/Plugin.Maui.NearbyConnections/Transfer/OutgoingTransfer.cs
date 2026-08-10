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

    CancellationTokenSource _inactivityCts = new(inactivityTimeout, timeProvider);
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
    /// A no-op once disposed. Platform callbacks arrive on their own thread and can land after the
    /// <c>finally</c> in <c>PlatformSendFileAsync</c> has already disposed this transfer; without the
    /// guard, the swap below installed a fresh <see cref="CancellationTokenSource"/> — with a live
    /// timer — that nothing would ever dispose, and reported progress for a transfer whose caller had
    /// already been handed a timeout exception.
    /// </remarks>
    public void OnUpdate(NearbyTransferProgress transferProgress)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var old = _inactivityCts;
            _inactivityCts = new CancellationTokenSource(inactivityTimeout, timeProvider);
            old.Dispose();
        }

        progress?.Report(transferProgress);

        switch (transferProgress.Status)
        {
            case NearbyTransferStatus.Success:
                _tcs.TrySetResult();
                break;
            case NearbyTransferStatus.Failure:
                _tcs.TrySetException(
                    new InvalidOperationException($"Transfer {transferProgress.PayloadId} failed."));
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
