using System.Threading.Channels;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.UnitTests.Helpers;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Advertiser")]
public sealed class ReceiveAllAsyncAdvertiserTests
{
    // ---------------------------------------------------------------------------
    // FakeNearbyConnections — advertise channel backed by a test-writable channel.
    // ---------------------------------------------------------------------------
    private sealed class FakeNearbyConnections : INearbyConnections
    {
        readonly Channel<NearbyConnectionRequest> _advertiseChannel =
            Channel.CreateUnbounded<NearbyConnectionRequest>();

        public void WriteRequest(NearbyConnectionRequest request)
            => _advertiseChannel.Writer.TryWrite(request);

        public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var req in _advertiseChannel.Reader.ReadAllAsync(cancellationToken))
                yield return req;
        }

        public IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(CancellationToken cancellationToken = default)
            => Channel.CreateUnbounded<NearbyDeviceEvent>().Reader.ReadAllAsync(cancellationToken);

        public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
            => Task.FromException<NearbyConnection>(new NotSupportedException());

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

    static NearbyConnectionRequest CreateRequest(NearbyConnection conn)
        => new(
            conn.RemoteDevice,
            acceptFactory: _ => Task.FromResult(conn),
            rejectFactory: _ => Task.CompletedTask);

    static async Task WaitForAsync(Func<bool> condition, int maxMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.IsTrue(condition(), $"Condition not met within {maxMs} ms.");
    }

    [TestMethod]
    public async Task ReceiveAllAsync_PayloadFromAcceptedConnection_ArrivesOnUnifiedStream()
    {
        // Arrange
        var fake = new FakeNearbyConnections();
        var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
        await advertiser.StartAsync();

        var (conn, receiveChannel) = CreateConnection();
        var request = CreateRequest(conn);
        fake.WriteRequest(request);

        await WaitForAsync(() => advertiser.PendingRequests.Count == 1);
        await advertiser.AcceptAsync(request);

        // Write a payload to the connection's receive channel (ForwardPayloadsAsync will pick it up)
        var payload = new BytesPayload([1, 2, 3]);
        receiveChannel.Writer.TryWrite(payload);

        // Act — read one item from the unified stream with a timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        (NearbyConnection Connection, NearbyPayload Payload) received = default;

        var readTask = Task.Run(async () =>
        {
            await foreach (var item in advertiser.ReceiveAllAsync(cts.Token))
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

        await advertiser.StopAsync();
        conn.CompleteReceive();
    }

    [TestMethod]
    public async Task ReceiveAllAsync_PayloadsFromMultipleConnections_AllArrive()
    {
        // Arrange
        var fake = new FakeNearbyConnections();
        var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
        await advertiser.StartAsync();

        var (conn1, ch1) = CreateConnection(new NearbyDevice("peer-1", "Alice"));
        var (conn2, ch2) = CreateConnection(new NearbyDevice("peer-2", "Bob"));

        var request1 = CreateRequest(conn1);
        var request2 = CreateRequest(conn2);
        fake.WriteRequest(request1);
        await WaitForAsync(() => advertiser.PendingRequests.Count >= 1);
        await advertiser.AcceptAsync(request1);

        fake.WriteRequest(request2);
        await WaitForAsync(() => advertiser.PendingRequests.Count >= 1);
        await advertiser.AcceptAsync(request2);

        var payload1 = new BytesPayload([10]);
        var payload2 = new BytesPayload([20]);
        ch1.Writer.TryWrite(payload1);
        ch2.Writer.TryWrite(payload2);

        // Act — collect two items from the unified stream
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var results = new List<(NearbyConnection Connection, NearbyPayload Payload)>();

        var readTask = Task.Run(async () =>
        {
            await foreach (var item in advertiser.ReceiveAllAsync(cts.Token))
            {
                results.Add(item);
                if (results.Count >= 2)
                    break;
            }
        }, cts.Token);

        await readTask.WaitAsync(cts.Token);

        // Assert
        Assert.AreEqual(2, results.Count);
        // Both payloads must be present (order is non-deterministic across connections)
        Assert.IsTrue(results.Any(r => ReferenceEquals(r.Payload, payload1)));
        Assert.IsTrue(results.Any(r => ReferenceEquals(r.Payload, payload2)));

        await advertiser.StopAsync();
        conn1.CompleteReceive();
        conn2.CompleteReceive();
    }

    [TestMethod]
    public async Task ReceiveAllAsync_ExitsCleanlyCancelTokenCanceled()
    {
        // Arrange
        var fake = new FakeNearbyConnections();
        var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
        await advertiser.StartAsync();

        using var cts = new CancellationTokenSource();

        var enumerateTask = Task.Run(async () =>
        {
            await foreach (var _ in advertiser.ReceiveAllAsync(cts.Token))
            {
                // consume
            }
        });

        // Act — cancel the token
        cts.Cancel();

        // Assert — enumerateTask completes (does not hang); OperationCanceledException is expected
        var completedTask = await Task.WhenAny(enumerateTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(enumerateTask, completedTask, "ReceiveAllAsync did not exit within 2 s after cancellation.");

        // The task should be canceled or faulted with OperationCanceledException — either is acceptable.
        if (enumerateTask.IsFaulted)
        {
            Assert.IsInstanceOfType<OperationCanceledException>(enumerateTask.Exception!.InnerException);
        }

        await advertiser.StopAsync();
    }

    [TestMethod]
    public async Task ReceiveAllAsync_PayloadWrittenBeforeDisconnect_IsNotLost()
    {
        // Arrange
        var fake = new FakeNearbyConnections();
        var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
        await advertiser.StartAsync();

        var (conn, receiveChannel) = CreateConnection();
        var request = CreateRequest(conn);
        fake.WriteRequest(request);

        await WaitForAsync(() => advertiser.PendingRequests.Count == 1);
        await advertiser.AcceptAsync(request);

        // Write payload BEFORE calling CompleteReceive — the channel is unbounded so it won't block
        var payload = new BytesPayload([99]);
        receiveChannel.Writer.TryWrite(payload);

        // Then disconnect
        conn.CompleteReceive();

        // Act — the payload must appear on the unified stream before the connection is removed
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        NearbyPayload? received = null;

        var readTask = Task.Run(async () =>
        {
            await foreach (var item in advertiser.ReceiveAllAsync(cts.Token))
            {
                received = item.Payload;
                break;
            }
        }, cts.Token);

        await readTask.WaitAsync(cts.Token);

        // Assert
        Assert.IsNotNull(received);
        Assert.AreSame(payload, received);

        await advertiser.StopAsync();
    }
}
