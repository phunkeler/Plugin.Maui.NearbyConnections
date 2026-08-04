using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// A controllable <see cref="INearbyConnections"/> for driving <see cref="NearbySession"/> from
/// tests: emit discovery events and inbound requests on demand, and decide what connecting does.
/// </summary>
/// <remarks>
/// One shared harness, replacing the five that were re-declared across the two deleted mirror test
/// files. Deliberately hand-written rather than a mock: the session consumes
/// <see cref="IAsyncEnumerable{T}"/> streams whose completion and fault timing are the thing under
/// test, and that is clearer to drive through a channel than to configure on a mock.
/// </remarks>
sealed class FakeNearbyConnections : INearbyConnections
{
    readonly Channel<NearbyConnectionRequest> _requests = Channel.CreateUnbounded<NearbyConnectionRequest>();
    readonly Channel<NearbyDeviceEvent> _deviceEvents = Channel.CreateUnbounded<NearbyDeviceEvent>();

    readonly TaskCompletionSource _advertisePumpFaulted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource _discoverPumpFaulted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets how many times advertising was started, to assert repeat starts are no-ops.</summary>
    public int AdvertiseCallCount { get; private set; }

    /// <summary>Gets how many times discovery was started.</summary>
    public int DiscoverCallCount { get; private set; }

    /// <summary>Set to make the advertise stream fault, simulating a platform start failure.</summary>
    public Exception? AdvertiseFault { get; init; }

    /// <summary>Set to make the discover stream fault, simulating a platform start failure.</summary>
    public Exception? DiscoverFault { get; init; }

    /// <summary>The connection <see cref="ConnectAsync"/> resolves to.</summary>
    public NearbyConnection? ConnectResult { get; set; }

    /// <summary>Set to make <see cref="ConnectAsync"/> throw, simulating a rejected connection.</summary>
    public Exception? ConnectFault { get; init; }

    public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AdvertiseCallCount++;

        if (AdvertiseFault is not null)
        {
            _advertisePumpFaulted.TrySetResult();
            throw AdvertiseFault;
        }

        await foreach (var request in _requests.Reader.ReadAllAsync(cancellationToken))
        {
            yield return request;
        }
    }

    public async IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DiscoverCallCount++;

        if (DiscoverFault is not null)
        {
            _discoverPumpFaulted.TrySetResult();
            throw DiscoverFault;
        }

        await foreach (var deviceEvent in _deviceEvents.Reader.ReadAllAsync(cancellationToken))
        {
            yield return deviceEvent;
        }
    }

    public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (ConnectFault is not null)
        {
            return Task.FromException<NearbyConnection>(ConnectFault);
        }

        return Task.FromResult(ConnectResult ?? throw new InvalidOperationException(
            $"{nameof(ConnectResult)} was not set on the fake."));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Emits a device-found event and waits for the session to apply it.</summary>
    public async Task EmitDeviceFoundAsync(NearbyDevice device)
    {
        _deviceEvents.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Found));
        await DrainAsync();
    }

    /// <summary>Emits a device-lost event and waits for the session to apply it.</summary>
    public async Task EmitDeviceLostAsync(NearbyDevice device)
    {
        _deviceEvents.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Lost));
        await DrainAsync();
    }

    /// <summary>
    /// Emits an inbound connection request and waits for the session to surface it.
    /// </summary>
    /// <param name="device">The requesting device.</param>
    /// <param name="onAccept">What accepting produces — return a connection or throw.</param>
    /// <param name="onReject">Optional callback invoked when the request is rejected.</param>
    public async Task EmitRequestAsync(
        NearbyDevice device,
        Func<NearbyConnection> onAccept,
        Action? onReject = null)
    {
        _requests.Writer.TryWrite(new NearbyConnectionRequest(
            device,
            acceptFactory: _ =>
            {
                try
                {
                    return Task.FromResult(onAccept());
                }
                catch (Exception ex)
                {
                    return Task.FromException<NearbyConnection>(ex);
                }
            },
            rejectFactory: _ =>
            {
                onReject?.Invoke();
                return Task.CompletedTask;
            }));

        await DrainAsync();
    }

    /// <summary>Waits for the advertise pump to observe <see cref="AdvertiseFault"/>.</summary>
    public Task WaitForAdvertisePumpAsync() => WaitWithTimeoutAsync(_advertisePumpFaulted.Task);

    /// <summary>Waits for the discover pump to observe <see cref="DiscoverFault"/>.</summary>
    public Task WaitForDiscoverPumpAsync() => WaitWithTimeoutAsync(_discoverPumpFaulted.Task);

    /// <summary>
    /// Yields until the session's pump has drained what was just written. The pump reads on a
    /// background task, so a test that asserted immediately would race it.
    /// </summary>
    static async Task DrainAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Yield();
        }

        await Task.Delay(10);
    }

    static async Task WaitWithTimeoutAsync(Task task)
    {
        await task.WaitAsync(TimeSpan.FromSeconds(5));

        // The pump sets its toggle after the fault surfaces; let that continuation run.
        await Task.Delay(20);
    }
}
