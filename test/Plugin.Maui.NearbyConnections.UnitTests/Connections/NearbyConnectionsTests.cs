using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Plugin.Maui.NearbyConnections;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class NearbyConnectionsTests
{
    // Builds a NearbyConnectionsImplementation wired to a no-op device manager
    // without hitting any platform APIs.
    static NearbyConnectionsImplementation CreateSut(FakeTimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? new FakeTimeProvider();
        var deviceManager = new NearbyDeviceManager(tp);
        return new NearbyConnectionsImplementation(
            deviceManager,
            tp,
            new NearbyConnectionsOptions(),
            NullLogger.Instance);
    }

    // Drains the first N items from the channel's reader via the internal channel.
    // Because PlatformStartAdvertisingAsync / PlatformStartDiscoveringAsync throw
    // PlatformNotSupportedException on net10.0, we exercise the channel bridge
    // helpers directly (WriteDeviceFound, WriteConnectionRequest, etc.) and read
    // from the channel reader rather than going through AdvertiseAsync/DiscoverAsync.

    [TestClass]
    public sealed class WriteConnectionRequestTests : NearbyConnectionsTests
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
    public sealed class ResolveConnectionTcsTests : NearbyConnectionsTests
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
                sendBytesFactory: (_, _) => Task.CompletedTask,
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
                sendBytesFactory: (_, _) => Task.CompletedTask,
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
    }

    [TestClass]
    public sealed class WriteDeviceFoundTests : NearbyConnectionsTests
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
    public sealed class WritePayloadTests : NearbyConnectionsTests
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
                sendBytesFactory: (_, _) => Task.CompletedTask,
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

            // Act & Assert — unknown peer silently drops
            sut.WritePayload("nonexistent-peer", payload);
        }
    }

    [TestClass]
    public sealed class DisposeAsyncTests : NearbyConnectionsTests
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

            // Act & Assert
            await sut.DisposeAsync();
            await sut.DisposeAsync();
        }
    }
}
