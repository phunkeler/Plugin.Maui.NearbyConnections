using System.Threading.Channels;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.UnitTests.Helpers;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Advertiser")]
public sealed class NearbyAdvertiserTests
{
    // ---------------------------------------------------------------------------
    // FakeNearbyConnections — backed by channels that test methods write to.
    // ---------------------------------------------------------------------------
    private sealed class FakeNearbyConnections : INearbyConnections
    {
        readonly Channel<NearbyConnectionRequest> _advertiseChannel =
            Channel.CreateUnbounded<NearbyConnectionRequest>();

        readonly Channel<NearbyDeviceEvent> _discoverChannel =
            Channel.CreateUnbounded<NearbyDeviceEvent>();

        public TaskCompletionSource<NearbyConnection> ConnectTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void WriteRequest(NearbyConnectionRequest request)
            => _advertiseChannel.Writer.TryWrite(request);

        public void CompleteAdvertise()
            => _advertiseChannel.Writer.TryComplete();

        public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var req in _advertiseChannel.Reader.ReadAllAsync(cancellationToken))
                yield return req;
        }

        public IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(CancellationToken cancellationToken = default)
            => _discoverChannel.Reader.ReadAllAsync(cancellationToken);

        public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
            => ConnectTcs.Task.WaitAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // ---------------------------------------------------------------------------
    // Helper: create a NearbyConnection with a writable channel.
    // ---------------------------------------------------------------------------
    static (NearbyConnection Connection, Channel<NearbyPayload> ReceiveChannel) CreateConnection(
        NearbyDevice? device = null)
    {
        var ch = Channel.CreateUnbounded<NearbyPayload>();
        var conn = new NearbyConnection(
            device ?? new NearbyDevice("peer-1", "Alice"),
            ch,
            sendBytesFactory: (_, _) => Task.CompletedTask,
            sendFileFactory: (_, _, _) => Task.CompletedTask,
            disposeFactory: () => ValueTask.CompletedTask);
        return (conn, ch);
    }

    // ---------------------------------------------------------------------------
    // Helper: create a NearbyConnectionRequest that immediately resolves to conn.
    // ---------------------------------------------------------------------------
    static NearbyConnectionRequest CreateRequest(NearbyConnection conn, NearbyDevice? device = null)
        => new(
            device ?? conn.RemoteDevice,
            acceptFactory: _ => Task.FromResult(conn),
            rejectFactory: _ => Task.CompletedTask);

    // ---------------------------------------------------------------------------
    // Helper: wait with a short polling loop for a condition to become true.
    // ---------------------------------------------------------------------------
    static async Task WaitForAsync(Func<bool> condition, int maxMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.IsTrue(condition(), $"Condition not met within {maxMs} ms.");
    }

    // ===========================================================================
    // StartAsync tests
    // ===========================================================================
    [TestClass]
    public sealed class StartAsyncTests
    {
        [TestMethod]
        public async Task StartAsync_SetsIsAdvertising_True()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);

            // Act
            await advertiser.StartAsync();

            // Assert
            Assert.IsTrue(advertiser.IsAdvertising);

            await advertiser.StopAsync();
        }
    }

    // ===========================================================================
    // StopAsync tests
    // ===========================================================================
    [TestClass]
    public sealed class StopAsyncTests
    {
        [TestMethod]
        public async Task StopAsync_SetsIsAdvertising_False()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
            await advertiser.StartAsync();
            Assert.IsTrue(advertiser.IsAdvertising);

            // Act
            await advertiser.StopAsync();

            // The RunLoopAsync finally block sets IsAdvertising = false on a background task;
            // allow a short window for it to complete.
            await WaitForAsync(() => !advertiser.IsAdvertising);

            // Assert
            Assert.IsFalse(advertiser.IsAdvertising);
        }

        [TestMethod]
        public async Task StartAsync_WhenCalledTwice_CancelsPreviousLoop()
        {
            // Arrange — first loop; use a slow channel so it stays open
            var fake1 = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake1, SynchronousDispatcher.Dispatch);
            await advertiser.StartAsync();
            Assert.IsTrue(advertiser.IsAdvertising);

            // Act — second StartAsync cancels the first and starts a new loop
            var fake2 = new FakeNearbyConnections();
            // The advertiser internally holds the CTS; calling StartAsync twice on the same
            // advertiser cancels the previous CTS. We just verify it doesn't throw and
            // IsAdvertising stays true.
            await advertiser.StartAsync();

            // Assert
            Assert.IsTrue(advertiser.IsAdvertising);

            await advertiser.StopAsync();
        }
    }

    // ===========================================================================
    // AcceptAsync tests
    // ===========================================================================
    [TestClass]
    public sealed class AcceptAsyncTests
    {
        [TestMethod]
        public async Task AcceptAsync_AddsConnectionToActiveConnections()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
            await advertiser.StartAsync();

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            // Wait for PendingRequests to contain the request
            await WaitForAsync(() => advertiser.PendingRequests.Count == 1);

            // Act
            await advertiser.AcceptAsync(request);

            // Assert
            Assert.AreEqual(1, advertiser.ActiveConnections.Count);

            await advertiser.StopAsync();
            conn.CompleteReceive();
        }

        [TestMethod]
        public async Task AcceptAsync_RemovesRequestFromPendingRequests()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
            await advertiser.StartAsync();

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => advertiser.PendingRequests.Count == 1);

            // Act
            await advertiser.AcceptAsync(request);

            // Assert
            Assert.AreEqual(0, advertiser.PendingRequests.Count);

            await advertiser.StopAsync();
            conn.CompleteReceive();
        }
    }

    // ===========================================================================
    // RejectAsync tests
    // ===========================================================================
    [TestClass]
    public sealed class RejectAsyncTests
    {
        [TestMethod]
        public async Task RejectAsync_RemovesRequestFromPendingRequests()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
            await advertiser.StartAsync();

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => advertiser.PendingRequests.Count == 1);

            // Act
            await advertiser.RejectAsync(request);

            // Assert
            Assert.AreEqual(0, advertiser.PendingRequests.Count);

            await advertiser.StopAsync();
        }
    }

    // ===========================================================================
    // MonitorConnection tests
    // ===========================================================================
    [TestClass]
    public sealed class MonitorConnectionTests
    {
        [TestMethod]
        public async Task MonitorConnection_RemovesFromActiveConnectionsWhenDisconnected()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake, SynchronousDispatcher.Dispatch);
            await advertiser.StartAsync();

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => advertiser.PendingRequests.Count == 1);
            await advertiser.AcceptAsync(request);
            Assert.AreEqual(1, advertiser.ActiveConnections.Count);

            // Act — trigger disconnect by completing the connection
            conn.CompleteReceive();

            // Assert — MonitorConnectionAsync runs as a fire-and-forget task;
            // allow a short window for the collection update.
            await WaitForAsync(() => advertiser.ActiveConnections.Count == 0);

            Assert.AreEqual(0, advertiser.ActiveConnections.Count);

            await advertiser.StopAsync();
        }
    }
}
