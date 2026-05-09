namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tracks a single outgoing file transfer, routing progress through a shared
/// <see cref="IProgress{T}"/> and completing an awaitable task when the transfer
/// reaches a terminal state.
/// </summary>
internal sealed class OutgoingTransfer(
    IProgress<NearbyTransferProgress>? progress,
    TimeSpan inactivityTimeout) : IDisposable
{
    readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationTokenSource _inactivityCts = new(inactivityTimeout);

    /// <summary>Awaitable task that completes when the transfer reaches a terminal state.</summary>
    public Task Completion => _tcs.Task;

    /// <summary>
    /// Cancelled when no transfer updates have been received within the configured inactivity
    /// timeout. Reset on every call to <see cref="OnUpdate"/>. Pass
    /// <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public CancellationToken InactivityToken => _inactivityCts.Token;

    /// <summary>Called by platform code to report a progress update or terminal status.</summary>
    public void OnUpdate(NearbyTransferProgress transferProgress)
    {
        var old = Interlocked.Exchange(ref _inactivityCts, new CancellationTokenSource(inactivityTimeout));
        old.Dispose();

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
        _inactivityCts.Dispose();
        GC.SuppressFinalize(this);
    }
}
