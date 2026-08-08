using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Plugin.Maui.NearbyConnections;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class PlatformNearbyConnectionsTests
{
    // Builds a PlatformNearbyConnections without hitting any platform APIs.
    static PlatformNearbyConnections CreateSut(
        FakeTimeProvider? timeProvider = null,
        NearbyConnectionsOptions? options = null)
    {
        var tp = timeProvider ?? new FakeTimeProvider();
        return new PlatformNearbyConnections(
            tp,
            options ?? new NearbyConnectionsOptions(),
            NullLogger.Instance);
    }

    // Drains the first N items from the channel's reader via the internal channel.
    // Because PlatformStartAdvertisingAsync / PlatformStartDiscoveringAsync throw
    // PlatformNotSupportedException on net10.0, we exercise the channel bridge
    // helpers directly (WriteDeviceFound, WriteConnectionRequest, etc.) and read
    // from the channel reader rather than going through AdvertiseAsync/DiscoverAsync.

    [TestClass]
    public sealed class WriteConnectionRequest : PlatformNearbyConnectionsTests
    {
        [TestMethod]
        public async Task WriteConnectionRequest_YieldsRequestOnAdvertiseChannel()
        {
            // Arrange
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            NearbyConnectionRequest? captured = null;
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            var request = new NearbyConnectionRequest(
                device,
                acceptFactory: ct => tcs.Task.WaitAsync(ct),
                rejectFactory: ct => Task.CompletedTask);

            // Act
            sut.WriteConnectionRequest(request);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            // Read one item directly from the internal advertise channel reader
            var reader = sut._advertiseChannel.Reader;
            captured = await reader.ReadAsync(cts.Token);

            // Assert
            Assert.IsNotNull(captured);
            Assert.AreSame(device, captured.RemoteDevice);
        }

        [TestMethod]
        public async Task WriteConnectionRequest_MultipleRequests_AllYielded()
        {
            // Arrange
            var sut = CreateSut();
            var device1 = new NearbyDevice("peer-1", "Alice");
            var device2 = new NearbyDevice("peer-2", "Bob");

            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            sut.WriteConnectionRequest(new NearbyConnectionRequest(device1, ct => tcs.Task.WaitAsync(ct), ct => Task.CompletedTask));
            sut.WriteConnectionRequest(new NearbyConnectionRequest(device2, ct => tcs.Task.WaitAsync(ct), ct => Task.CompletedTask));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var reader = sut._advertiseChannel.Reader;

            // Act
            var first = await reader.ReadAsync(cts.Token);
            var second = await reader.ReadAsync(cts.Token);

            // Assert
            Assert.AreEqual("peer-1", first.RemoteDevice.Id);
            Assert.AreEqual("peer-2", second.RemoteDevice.Id);
        }
    }

    [TestClass]
    public sealed class ResolveConnectionTcs : PlatformNearbyConnectionsTests
    {
        [TestMethod]
        public async Task AcceptAsync_ResolveConnectionTcs_CompletesWithNearbyConnection()
        {
            // Arrange
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            // Simulate what ConnectAsync does: register a TCS keyed by peer ID
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            sut._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = new NearbyConnection(
                device,
                receiveChannel,
                sendBytesFactory: (_, _) => ValueTask.CompletedTask,
                sendFileFactory: (_, _, _) => Task.CompletedTask,
                disposeFactory: () => ValueTask.CompletedTask);

            // Act — simulate platform callback resolving the TCS
            sut.ResolveConnectionTcs("peer-1", connection);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var result = await tcs.Task.WaitAsync(cts.Token);

            // Assert
            Assert.AreSame(connection, result);
        }

        [TestMethod]
        public async Task ResolveConnectionTcs_RegistersConnectionInActiveConnections()
        {
            // Arrange
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            sut._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = new NearbyConnection(
                device,
                receiveChannel,
                sendBytesFactory: (_, _) => ValueTask.CompletedTask,
                sendFileFactory: (_, _, _) => Task.CompletedTask,
                disposeFactory: () => ValueTask.CompletedTask);

            // Act
            sut.ResolveConnectionTcs("peer-1", connection);
            await tcs.Task; // wait for resolution

            // Assert — connection is now tracked in _activeConnections
            Assert.IsTrue(sut._activeConnections.ContainsKey("peer-1"));
        }

        [TestMethod]
        public async Task FaultConnectionTcs_FaultsTheTcsWithGivenException()
        {
            // Arrange
            var sut = CreateSut();
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            sut._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var expectedException = new InvalidOperationException("connection failed");

            // Act
            sut.FaultConnectionTcs("peer-1", expectedException);

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await tcs.Task);
        }

        [TestMethod]
        public void ResolveConnectionTcs_NoRegisteredTcs_SilentlyNoOps()
        {
            // Arrange - reproduces the iOS advertiser race window where a platform callback
            // (e.g. MCSessionState.Connected) can fire before the acceptFactory continuation
            // has registered its TCS in _connectionTcs. Callers must register the TCS before
            // triggering any platform operation that could resolve it; this test pins down
            // that ResolveConnectionTcs offers no rescue if that ordering is violated.
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = new NearbyConnection(
                device,
                receiveChannel,
                sendBytesFactory: (_, _) => ValueTask.CompletedTask,
                sendFileFactory: (_, _, _) => Task.CompletedTask,
                disposeFactory: () => ValueTask.CompletedTask);

            // Act
            sut.ResolveConnectionTcs("peer-1", connection);

            // Assert - no TCS was registered, so the resolution is dropped and the connection
            // is never tracked as active; nothing throws.
            Assert.IsFalse(sut._activeConnections.ContainsKey("peer-1"));
        }
    }

    [TestClass]
    public sealed class WriteDeviceFound : PlatformNearbyConnectionsTests
    {
        [TestMethod]
        public async Task WriteDeviceFound_YieldsFoundEventOnDiscoverChannel()
        {
            // Arrange
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            sut.WriteDeviceFound(device);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var evt = await sut._discoverChannel.Reader.ReadAsync(cts.Token);

            // Assert
            Assert.AreEqual(NearbyDeviceEventType.Found, evt.Type);
            Assert.AreSame(device, evt.Device);
        }

        [TestMethod]
        public async Task WriteDeviceLost_YieldsLostEventOnDiscoverChannel()
        {
            // Arrange
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            sut.WriteDeviceLost(device);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var evt = await sut._discoverChannel.Reader.ReadAsync(cts.Token);

            // Assert
            Assert.AreEqual(NearbyDeviceEventType.Lost, evt.Type);
            Assert.AreSame(device, evt.Device);
        }

        [TestMethod]
        public async Task WriteDeviceFound_ThenLost_PreservesOrder()
        {
            // Arrange
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            sut.WriteDeviceFound(device);
            sut.WriteDeviceLost(device);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var reader = sut._discoverChannel.Reader;
            var first = await reader.ReadAsync(cts.Token);
            var second = await reader.ReadAsync(cts.Token);

            // Assert
            Assert.AreEqual(NearbyDeviceEventType.Found, first.Type);
            Assert.AreEqual(NearbyDeviceEventType.Lost, second.Type);
        }
    }

    [TestClass]
    public sealed class WritePayload : PlatformNearbyConnectionsTests
    {
        [TestMethod]
        public async Task WritePayload_RoutesPayloadToActiveConnection()
        {
            // Arrange
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");

            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            sut._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = new NearbyConnection(
                device,
                receiveChannel,
                sendBytesFactory: (_, _) => ValueTask.CompletedTask,
                sendFileFactory: (_, _, _) => Task.CompletedTask,
                disposeFactory: () => ValueTask.CompletedTask);

            sut.ResolveConnectionTcs("peer-1", connection);
            await tcs.Task;

            var payload = new BytesPayload([1, 2, 3]);

            // Act
            sut.WritePayload("peer-1", payload);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var received = await receiveChannel.Reader.ReadAsync(cts.Token);

            // Assert
            Assert.AreSame(payload, received);
        }

        [TestMethod]
        public void WritePayload_UnknownPeer_DoesNotThrow()
        {
            // Arrange
            var sut = CreateSut();
            var payload = new BytesPayload([1, 2, 3]);

            // Act
            sut.WritePayload("nonexistent-peer", payload);

            // Assert — unknown peer silently ignored; no connection was registered
            Assert.IsFalse(sut._activeConnections.ContainsKey("nonexistent-peer"));
        }
    }

    [TestClass]
    public sealed class AllowSynchronousContinuations : PlatformNearbyConnectionsTests
    {
        [TestMethod]
        public void False_WriteReturnsBeforeAwaitingReaderContinuationRuns()
        {
            // Arrange — default options: AllowSynchronousContinuations is false, so the
            // channel schedules the waiting reader's continuation to the thread pool
            // instead of running it inline on the writer's call stack.
            var sut = CreateSut();
            var device = new NearbyDevice("peer-1", "Alice");
            var continuationRan = false;

            var readValueTask = sut._discoverChannel.Reader.ReadAsync();
            _ = readValueTask.AsTask().ContinueWith(
                _ => continuationRan = true,
                TaskContinuationOptions.ExecuteSynchronously);

            // Act
            sut.WriteDeviceFound(device);

            // Assert — WriteDeviceFound (a synchronous TryWrite) has already returned, but
            // the continuation was scheduled rather than run inline, so it hasn't run yet.
            Assert.IsFalse(continuationRan);
        }

        [TestMethod]
        public void True_WriteRunsAwaitingReaderContinuationInline()
        {
            // Arrange
            var options = new NearbyConnectionsOptions { AllowSynchronousContinuations = true };
            var sut = CreateSut(options: options);
            var device = new NearbyDevice("peer-1", "Alice");
            var continuationRan = false;

            var readValueTask = sut._discoverChannel.Reader.ReadAsync();
            _ = readValueTask.AsTask().ContinueWith(
                _ => continuationRan = true,
                TaskContinuationOptions.ExecuteSynchronously);

            // Act
            sut.WriteDeviceFound(device);

            // Assert — the continuation ran synchronously, inline within TryWrite,
            // before WriteDeviceFound returned.
            Assert.IsTrue(continuationRan);
        }
    }

    [TestClass]
    public sealed class DisposeAsync : PlatformNearbyConnectionsTests
    {
        [TestMethod]
        public async Task DisposeAsync_CompletesAdvertiseChannel()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            await sut.DisposeAsync();

            // Assert — channel writer is completed, so reader will complete immediately
            Assert.IsTrue(sut._advertiseChannel.Reader.Completion.IsCompleted);
        }

        [TestMethod]
        public async Task DisposeAsync_CompletesDiscoverChannel()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            await sut.DisposeAsync();

            // Assert
            Assert.IsTrue(sut._discoverChannel.Reader.Completion.IsCompleted);
        }

        [TestMethod]
        public async Task DisposeAsync_CancelsPendingConnectionTcs()
        {
            // Arrange
            var sut = CreateSut();
            using var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            sut._connectionTcs["peer-1"] = (tcs, cts.Token);

            // Act
            await sut.DisposeAsync();

            // Assert
            Assert.IsTrue(tcs.Task.IsCanceled);
        }

        [TestMethod]
        public async Task DisposeAsync_CalledTwice_DoesNotThrow()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            await sut.DisposeAsync();
#pragma warning disable S3966 // intentional: second call verifies idempotency
            await sut.DisposeAsync();
#pragma warning restore S3966

            // Assert
            Assert.IsTrue(sut._advertiseChannel.Reader.Completion.IsCompleted);
        }
    }
}
