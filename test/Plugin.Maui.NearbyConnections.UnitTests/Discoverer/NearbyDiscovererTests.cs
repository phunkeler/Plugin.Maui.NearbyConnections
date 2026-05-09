using System.Threading.Channels;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.UnitTests.Helpers;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Discoverer")]
public sealed class NearbyDiscovererTests
{
    // ---------------------------------------------------------------------------
    // FakeNearbyConnections — discover channel + ConnectAsync TCS backed by tests.
    // ---------------------------------------------------------------------------
    private sealed class FakeNearbyConnections : INearbyConnections
    {
        readonly Channel<NearbyDeviceEvent> _discoverChannel =
            Channel.CreateUnbounded<NearbyDeviceEvent>();

        public TaskCompletionSource<NearbyConnection> ConnectTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void WriteFound(NearbyDevice device)
            => _discoverChannel.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Found));

        public void WriteLost(NearbyDevice device)
            => _discoverChannel.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Lost));

        public IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(CancellationToken cancellationToken = default)
            => Channel.CreateUnbounded<NearbyConnectionRequest>().Reader.ReadAllAsync(cancellationToken);

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
        var ch = Channel.CreateUnbounded<NearbyPayload>();
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

    // ===========================================================================
    // StartAsync tests
    // ===========================================================================
    [TestClass]
    public sealed class StartAsyncTests
    {
        [TestMethod]
        public async Task StartAsync_SetsIsDiscovering_True()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);

            // Act
            await discoverer.StartAsync();

            // Assert
            Assert.IsTrue(discoverer.IsDiscovering);

            await discoverer.StopAsync();
        }
    }

    // ===========================================================================
    // StopAsync tests
    // ===========================================================================
    [TestClass]
    public sealed class StopAsyncTests
    {
        [TestMethod]
        public async Task StopAsync_SetsIsDiscovering_False()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
            await discoverer.StartAsync();
            Assert.IsTrue(discoverer.IsDiscovering);

            // Act
            await discoverer.StopAsync();

            // The RunLoopAsync finally block sets IsDiscovering = false on a background task.
            await WaitForAsync(() => !discoverer.IsDiscovering);

            // Assert
            Assert.IsFalse(discoverer.IsDiscovering);
        }
    }

    // ===========================================================================
    // Device discovery tests
    // ===========================================================================
    [TestClass]
    public sealed class DeviceDiscoveryTests
    {
        [TestMethod]
        public async Task DeviceFound_AddsDeviceToNearbyDevices()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
            await discoverer.StartAsync();

            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            fake.WriteFound(device);

            await WaitForAsync(() => discoverer.NearbyDevices.Count == 1);

            // Assert
            Assert.AreEqual(1, discoverer.NearbyDevices.Count);
            Assert.AreSame(device, discoverer.NearbyDevices[0]);

            await discoverer.StopAsync();
        }

        [TestMethod]
        public async Task DeviceLost_RemovesDeviceFromNearbyDevices()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
            await discoverer.StartAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            fake.WriteFound(device);
            await WaitForAsync(() => discoverer.NearbyDevices.Count == 1);

            // Act
            fake.WriteLost(device);

            await WaitForAsync(() => discoverer.NearbyDevices.Count == 0);

            // Assert
            Assert.AreEqual(0, discoverer.NearbyDevices.Count);

            await discoverer.StopAsync();
        }
    }

    // ===========================================================================
    // ConnectAsync tests
    // ===========================================================================
    [TestClass]
    public sealed class ConnectAsyncTests
    {
        [TestMethod]
        public async Task ConnectAsync_RemovesDeviceFromNearbyDevices()
        {
            // Arrange
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
            await discoverer.StartAsync();

            fake.WriteFound(device);
            await WaitForAsync(() => discoverer.NearbyDevices.Count == 1);

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);

            // Act
            await discoverer.ConnectAsync(device);

            // Assert
            Assert.AreEqual(0, discoverer.NearbyDevices.Count);

            await discoverer.StopAsync();
            conn.CompleteReceive();
        }

        [TestMethod]
        public async Task ConnectAsync_AddsConnectionToActiveConnections()
        {
            // Arrange
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
            await discoverer.StartAsync();

            fake.WriteFound(device);
            await WaitForAsync(() => discoverer.NearbyDevices.Count == 1);

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);

            // Act
            var result = await discoverer.ConnectAsync(device);

            // Assert
            Assert.AreEqual(1, discoverer.ActiveConnections.Count);
            Assert.AreSame(conn, result);

            await discoverer.StopAsync();
            conn.CompleteReceive();
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
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake, SynchronousDispatcher.Dispatch);
            await discoverer.StartAsync();

            fake.WriteFound(device);
            await WaitForAsync(() => discoverer.NearbyDevices.Count == 1);

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);
            await discoverer.ConnectAsync(device);

            Assert.AreEqual(1, discoverer.ActiveConnections.Count);

            // Act — trigger disconnect
            conn.CompleteReceive();

            // Assert — MonitorConnectionAsync is fire-and-forget; allow a short window.
            await WaitForAsync(() => discoverer.ActiveConnections.Count == 0);

            Assert.AreEqual(0, discoverer.ActiveConnections.Count);

            await discoverer.StopAsync();
        }
    }
}
