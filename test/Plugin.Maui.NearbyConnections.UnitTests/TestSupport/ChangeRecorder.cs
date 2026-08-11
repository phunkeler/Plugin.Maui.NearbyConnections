namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Records everything published to <see cref="INearbyDevices.Changes"/>, so a test can assert on the
/// sequence of transitions a device went through.
/// </summary>
/// <remarks>
/// Constructing one subscribes immediately, so a change raised after the constructor returns is
/// always captured. Starting the pump lazily instead would leave a window in which a change
/// published right after construction is lost — the pump has not reached its first
/// <c>MoveNextAsync</c> yet, so nothing is subscribed to receive it.
/// </remarks>
sealed class ChangeRecorder : IAsyncDisposable
{
    readonly List<NearbyDeviceChange> _changes = [];
    readonly CancellationTokenSource _cts = new();
    readonly IAsyncEnumerator<NearbyDeviceChange> _enumerator;
    readonly Task _pump;

    public ChangeRecorder(INearby session)
        : this(session.Devices.Changes)
    {
    }

    public ChangeRecorder(IAsyncEnumerable<NearbyDeviceChange> changes)
    {
        _enumerator = changes.GetAsyncEnumerator(_cts.Token);

        // Kick the enumerator here, synchronously, so the watcher's channel is registered before the
        // constructor returns. See the remarks above for why this cannot move into PumpAsync.
        var first = _enumerator.MoveNextAsync();
        _pump = PumpAsync(first);
    }

    /// <summary>Every change recorded so far, oldest first.</summary>
    public IReadOnlyList<NearbyDeviceChange> Changes
    {
        get { lock (_changes) { return [.. _changes]; } }
    }

    /// <summary>Every change recorded for one device, oldest first.</summary>
    public IReadOnlyList<NearbyDeviceChange> For(string deviceId)
        => [.. Changes.Where(c => c.Device.Id == deviceId)];

    /// <summary>The statuses a device has been reported in, oldest first.</summary>
    public IReadOnlyList<NearbyDeviceStatus> StatusesFor(string deviceId)
        => [.. For(deviceId).Select(c => c.Device.Status)];

    /// <summary>
    /// Waits until at least <paramref name="count"/> changes have been recorded for a device.
    /// Publishing hands the change to a channel that this pump drains on another thread, so an
    /// assertion made immediately after an operation can otherwise race the recording.
    /// </summary>
    public Task WaitForAsync(string deviceId, int count)
        => Wait.UntilAsync(() => For(deviceId).Count >= count);

    /// <summary>Waits until at least <paramref name="count"/> changes have been recorded in total.</summary>
    public Task WaitForAsync(int count)
        => Wait.UntilAsync(() => Changes.Count >= count);

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        try
        {
            await _pump;
        }
        catch (OperationCanceledException)
        {
            // Expected: cancelling is how the pump is stopped.
        }

        _cts.Dispose();
    }

    async Task PumpAsync(ValueTask<bool> first)
    {
        try
        {
            var hasNext = await first;

            while (hasNext)
            {
                lock (_changes)
                {
                    _changes.Add(_enumerator.Current);
                }

                hasNext = await _enumerator.MoveNextAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disposal.
        }
        finally
        {
            await _enumerator.DisposeAsync();
        }
    }
}
