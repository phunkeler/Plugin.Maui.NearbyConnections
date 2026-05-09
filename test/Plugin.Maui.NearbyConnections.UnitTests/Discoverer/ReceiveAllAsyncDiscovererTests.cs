using System.Threading.Channels;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.UnitTests.Helpers;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Discoverer")]
public sealed class ReceiveAllAsyncDiscovererTests
{
    // ---------------------------------------------------------------------------
    // FakeNearbyConnections — connect factory backed by a per-test TCS.
    // ---------------------------------------------------------------------------
    private sealed class FakeNearbyConnections : INearbyConnections
    {
        readonly Channel<NearbyDeviceEvent> _discoverChannel =
            Channel.CreateUnbounded<NearbyDeviceEvent>();

        readonly Channel<NearbyConnectionRequest> _advertiseChannel =
            Channel.CreateUnbounded<NearbyConnectionRequest>();

        public TaskCompletionSource<NearbyConnection> ConnectTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void WriteFound(NearbyDevice device)
            => _discoverChannel.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Found));

        public IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(CancellationToken cancellationToken = default)
            => _advertiseChannel.Reader.ReadAllAsync(cancellationToken);

        public async IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var ev in _discoverChannel.Reader.ReadAllAsync(cancellationToken))
                yield return ev;
        }

        public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
            => ConnectTcs.Task.WaitAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    static (NearbyConnection Connection, Channel<NearbyPayload> ReceiveChannel) CreateConnection(
        NearbyDevice? device = null)
    {
        var ch = Channel.CreateUnbounded<NearbyPayload>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var conn = new NearbyConnection(
            device ?? new NearbyDevice("peer-1", "Alice"),
            ch,
            sendBytesFactory: (_, _) => Task.CompletedTask,
            sendFileFactory: (_, _, _) => Task.CompletedTask,
            disposeFactory: () => ValueTask.CompletedTask);
        return (conn, ch);
    }

    static async Task WaitForAsync(Func<bool> condition, int maxMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.IsTrue(condition(), $"Condition not met within {maxMs} ms.");
    }

    // ---------------------------------------------------------------------------
    // Helper: start discovering, emit a Found event, connect, and return the conn.
    // ---------------------------------------------------------------------------
    static async Task<(NearbyDiscoverer Discoverer, NearbyConnection Connection, Channel<NearbyPayload> ReceiveChannel)>
        SetupConnectedDiscoverer(FakeNearbyConnections fake, NearbyDevice? device = null)
    {
        var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
        await discoverer.StartAsync();

        var d = device ?? new NearbyDevice("peer-1", "Alice");
        fake.WriteFound(d);
        await WaitForAsync(() => discoverer.NearbyDevices.Count >= 1);

        var (conn, ch) = CreateConnection(d);
        fake.ConnectTcs.SetResult(conn);

        await discoverer.ConnectAsync(d);
        return (discoverer, conn, ch);
    }

    [TestMethod]
    public async Task ReceiveAllAsync_PayloadFromConnectedPeer_ArrivesOnUnifiedStream()
    {
        // Arrange
        var fake = new FakeNearbyConnections();
        var (discoverer, conn, receiveChannel) = await SetupConnectedDiscoverer(fake);

        var payload = new BytesPayload([1, 2, 3]);
        receiveChannel.Writer.TryWrite(payload);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        (NearbyConnection Connection, NearbyPayload Payload) received = default;

        var readTask = Task.Run(async () =>
        {
            await foreach (var item in discoverer.ReceiveAllAsync(cts.Token))
            {
                received = item;
                break;
            }
        }, cts.Token);

        await readTask.WaitAsync(cts.Token);

        // Assert
        Assert.IsNotNull(received.Payload);
        Assert.AreSame(payload, received.Payload);
        Assert.AreSame(conn, received.Connection);

        await discoverer.StopAsync();
        conn.CompleteReceive();
    }

    [TestMethod]
    public async Task ReceiveAllAsync_PayloadsFromMultipleConnections_AllArrive()
    {
        // Arrange — two separate discoverer/fake pairs for two independent connections
        var device1 = new NearbyDevice("peer-1", "Alice");
        var device2 = new NearbyDevice("peer-2", "Bob");

        var (ch1, conn1) = CreateChannelAndConnection(device1);
        var (ch2, conn2) = CreateChannelAndConnection(device2);

        var fake = new MultiConnectFake(conn1, conn2);
        var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
        await discoverer.StartAsync();

        fake.WriteFound(device1);
        await WaitForAsync(() => discoverer.NearbyDevices.Count >= 1);
        await discoverer.ConnectAsync(device1);

        fake.WriteFound(device2);
        await WaitForAsync(() => discoverer.NearbyDevices.Count >= 1);
        await discoverer.ConnectAsync(device2);

        var payload1 = new BytesPayload([10]);
        var payload2 = new BytesPayload([20]);
        ch1.Writer.TryWrite(payload1);
        ch2.Writer.TryWrite(payload2);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var results = new List<(NearbyConnection Connection, NearbyPayload Payload)>();

        var readTask = Task.Run(async () =>
        {
            await foreach (var item in discoverer.ReceiveAllAsync(cts.Token))
            {
                results.Add(item);
                if (results.Count >= 2)
                    break;
            }
        }, cts.Token);

        await readTask.WaitAsync(cts.Token);

        // Assert
        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.Any(r => ReferenceEquals(r.Payload, payload1)));
        Assert.IsTrue(results.Any(r => ReferenceEquals(r.Payload, payload2)));

        await discoverer.StopAsync();
        conn1.CompleteReceive();
        conn2.CompleteReceive();
    }

    [TestMethod]
    public async Task ReceiveAllAsync_ExitsCleanlyCancelTokenCanceled()
    {
        // Arrange
        var fake = new FakeNearbyConnections();
        var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
        await discoverer.StartAsync();

        using var cts = new CancellationTokenSource();

        var enumerateTask = Task.Run(async () =>
        {
            await foreach (var _ in discoverer.ReceiveAllAsync(cts.Token))
            {
                // consume
            }
        });

        // Act — cancel
        cts.Cancel();

        // Assert — must exit within timeout
        var completedTask = await Task.WhenAny(enumerateTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(enumerateTask, completedTask, "ReceiveAllAsync did not exit within 2 s after cancellation.");

        if (enumerateTask.IsFaulted)
        {
            Assert.IsInstanceOfType<OperationCanceledException>(enumerateTask.Exception!.InnerException);
        }

        await discoverer.StopAsync();
    }

    [TestMethod]
    public async Task ReceiveAllAsync_PayloadWrittenBeforeDisconnect_IsNotLost()
    {
        // Arrange
        var fake = new FakeNearbyConnections();
        var (discoverer, conn, receiveChannel) = await SetupConnectedDiscoverer(fake);

        var payload = new BytesPayload([99]);
        receiveChannel.Writer.TryWrite(payload);

        // Disconnect after payload is already in the channel
        conn.CompleteReceive();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        NearbyPayload? received = null;

        var readTask = Task.Run(async () =>
        {
            await foreach (var item in discoverer.ReceiveAllAsync(cts.Token))
            {
                received = item.Payload;
                break;
            }
        }, cts.Token);

        await readTask.WaitAsync(cts.Token);

        // Assert
        Assert.IsNotNull(received);
        Assert.AreSame(payload, received);

        await discoverer.StopAsync();
    }

    // ---------------------------------------------------------------------------
    // Helpers for multi-connection test
    // ---------------------------------------------------------------------------
    static (Channel<NearbyPayload> Channel, NearbyConnection Connection) CreateChannelAndConnection(
        NearbyDevice device)
    {
        var ch = Channel.CreateUnbounded<NearbyPayload>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var conn = new NearbyConnection(
            device,
            ch,
            sendBytesFactory: (_, _) => Task.CompletedTask,
            sendFileFactory: (_, _, _) => Task.CompletedTask,
            disposeFactory: () => ValueTask.CompletedTask);
        return (ch, conn);
    }

    /// <summary>
    /// A fake that returns two pre-built connections in sequence — one per ConnectAsync call.
    /// </summary>
    private sealed class MultiConnectFake : INearbyConnections
    {
        readonly NearbyConnection[] _connections;
        int _index;

        readonly Channel<NearbyDeviceEvent> _discoverChannel =
            Channel.CreateUnbounded<NearbyDeviceEvent>();

        public MultiConnectFake(params NearbyConnection[] connections)
            => _connections = connections;

        public void WriteFound(NearbyDevice device)
            => _discoverChannel.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Found));

        public IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(CancellationToken cancellationToken = default)
            => Channel.CreateUnbounded<NearbyConnectionRequest>().Reader.ReadAllAsync(cancellationToken);

        public async IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var ev in _discoverChannel.Reader.ReadAllAsync(cancellationToken))
                yield return ev;
        }

        public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        {
            var conn = _connections[_index++];
            return Task.FromResult(conn);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
