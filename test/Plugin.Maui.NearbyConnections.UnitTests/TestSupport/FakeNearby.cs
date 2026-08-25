using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// A controllable <see cref="IPlatformNearby"/> for driving <see cref="NearbyImplementation"/> from
/// tests: emit discovery events and inbound requests on demand, and decide what connecting does.
/// </summary>
/// <remarks>
/// One shared harness, replacing the five that were re-declared across the two deleted mirror test
/// files. Deliberately hand-written rather than a mock: the session consumes
/// <see cref="IAsyncEnumerable{T}"/> streams whose completion and fault timing are the thing under
/// test, and that is clearer to drive through a channel than to configure on a mock.
/// </remarks>
sealed class FakeNearby : IPlatformNearby
{
    Channel<NearbyConnectionRequest> _requests = Channel.CreateUnbounded<NearbyConnectionRequest>();
    Channel<NearbyDeviceEvent> _deviceEvents = Channel.CreateUnbounded<NearbyDeviceEvent>();

    /// <summary>
    /// The fake's connection table — the platform side owns "device X has a live connection", so
    /// the fake tracks what its own <see cref="ConnectAsync"/> and emitted accepts produced.
    /// Mirrors the real platform: registered before the caller observes the connection, removed
    /// when the connection's <see cref="NearbyConnection.Disconnected"/> completes (the fake's
    /// stand-in for the release path).
    /// </summary>
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, NearbyConnection> _connections
        = new(StringComparer.Ordinal);

    public bool TryGetConnection(string deviceId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out NearbyConnection? connection)
        => _connections.TryGetValue(deviceId, out connection);

    public NearbyConnection[] SnapshotConnections() => [.. _connections.Values];

    void RegisterConnection(string deviceId, NearbyConnection connection)
    {
        _connections[deviceId] = connection;
        _ = RemoveOnDisconnectAsync(deviceId, connection);
    }

    async Task RemoveOnDisconnectAsync(string deviceId, NearbyConnection connection)
    {
        await connection.Disconnected.ConfigureAwait(false);
        _connections.TryRemove(new KeyValuePair<string, NearbyConnection>(deviceId, connection));
    }

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

    /// <summary>What <see cref="CheckAvailabilityAsync"/> reports.</summary>
    public NearbyAvailability Availability { get; set; } = NearbyAvailability.Ready;

    /// <summary>Gets how many times availability was checked.</summary>
    public int CheckAvailabilityCallCount { get; private set; }

    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckAvailabilityCallCount++;
        return Task.FromResult(Availability);
    }

    public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
        TaskCompletionSource started,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AdvertiseCallCount++;

        if (AdvertiseFault is not null)
        {
            started.SetException(AdvertiseFault);
            throw AdvertiseFault;
        }

        // A fresh channel per call, mirroring PlatformNearby.shared.cs's Interlocked.Exchange: a
        // restart after a prior FaultAdvertiseStream must reach a channel that isn't already faulted.
        var requests = Channel.CreateUnbounded<NearbyConnectionRequest>();
        _requests = requests;

        started.TrySetResult();

        await foreach (var request in requests.Reader.ReadAllAsync(cancellationToken))
        {
            yield return request;
        }
    }

    public async IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(
        TaskCompletionSource started,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DiscoverCallCount++;

        if (DiscoverFault is not null)
        {
            started.SetException(DiscoverFault);
            throw DiscoverFault;
        }

        var deviceEvents = Channel.CreateUnbounded<NearbyDeviceEvent>();
        _deviceEvents = deviceEvents;

        started.TrySetResult();

        await foreach (var deviceEvent in deviceEvents.Reader.ReadAllAsync(cancellationToken))
        {
            yield return deviceEvent;
        }
    }

    /// <summary>
    /// Faults the advertise stream after a successful start, simulating a later platform failure
    /// (e.g. the radio drops mid-session) rather than a start failure.
    /// </summary>
    public void FaultAdvertiseStream(Exception exception) => _requests.Writer.TryComplete(exception);

    /// <summary>Faults the discover stream after a successful start. See <see cref="FaultAdvertiseStream"/>.</summary>
    public void FaultDiscoverStream(Exception exception) => _deviceEvents.Writer.TryComplete(exception);

    public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (ConnectFault is not null)
        {
            return Task.FromException<NearbyConnection>(ConnectFault);
        }

        var connection = ConnectResult ?? throw new InvalidOperationException(
            $"{nameof(ConnectResult)} was not set on the fake.");

        // Registered before the caller observes it, mirroring ResolveConnectionTcs.
        RegisterConnection(device.Id, connection);

        return Task.FromResult(connection);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Emits a device-found event and waits for the session to apply it.</summary>
    public async Task EmitDeviceFoundAsync(NearbyDevice device)
    {
        _deviceEvents.Writer.TryWrite(new NearbyDeviceEvent(device, Found: true));
        await DrainAsync();
    }

    /// <summary>Emits a device-lost event and waits for the session to apply it.</summary>
    public async Task EmitDeviceLostAsync(NearbyDevice device)
    {
        _deviceEvents.Writer.TryWrite(new NearbyDeviceEvent(device, Found: false));
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
            accept: _ =>
            {
                try
                {
                    var connection = onAccept();
                    RegisterConnection(device.Id, connection);
                    return Task.FromResult(connection);
                }
                catch (Exception ex)
                {
                    return Task.FromException<NearbyConnection>(ex);
                }
            },
            reject: _ =>
            {
                onReject?.Invoke();
                return Task.CompletedTask;
            }));

        await DrainAsync();
    }

    TaskCompletionSource<NearbyConnection>? _capturedAccept;

    /// <summary>
    /// Hands back the source the next emitted accept will await, so a test can observe how that
    /// accept ended. Pair with <see cref="EmitRequestThatOnlyCancellationEndsAsync"/>.
    /// </summary>
    public TaskCompletionSource<NearbyConnection> CaptureNextAcceptToken()
        => _capturedAccept = new TaskCompletionSource<NearbyConnection>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Emits an inbound request whose accept never completes on its own, so only the token the
    /// session passes can release it.
    /// </summary>
    /// <remarks>
    /// The platform makes no promise that a handshake callback ever arrives, and auto-accept is
    /// started by a callback rather than a caller, so no caller's token reaches it. This is the
    /// shape that hangs disposal when the session has no disposal token of its own to pass.
    /// </remarks>
    /// <param name="device">The requesting device.</param>
    public async Task EmitRequestThatOnlyCancellationEndsAsync(NearbyDevice device)
    {
        _requests.Writer.TryWrite(new NearbyConnectionRequest(
            device,
            accept: ct =>
            {
                var tcs = _capturedAccept ?? new TaskCompletionSource<NearbyConnection>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                ct.Register(() => tcs.TrySetCanceled(ct));

                return tcs.Task;
            },
            reject: _ => Task.CompletedTask));

        await DrainAsync();
    }

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
}
