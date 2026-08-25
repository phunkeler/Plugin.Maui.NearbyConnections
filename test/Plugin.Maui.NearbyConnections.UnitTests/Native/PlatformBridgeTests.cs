using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the channel bridge platform callbacks write into: <c>WriteDeviceFound</c>,
/// <c>WriteConnectionRequest</c>, <c>ResolveConnectionTcs</c> and their siblings.
/// </summary>
/// <remarks>
/// <para>
/// <strong>These tests read internal fields, against this suite's usual rule of asserting only
/// through the surface a consumer uses.</strong> The natural observation point is
/// <c>AdvertiseAsync</c> / <c>DiscoverAsync</c>, but both call a <c>Platform*</c> start that throws
/// <see cref="PlatformNotSupportedException"/> on <c>net10.0</c>, so no enumeration can reach the
/// channel. Reading the channel directly is the only way to test this layer off-device.
/// </para>
/// <para>
/// The swap itself — <c>AdvertiseAsync</c> exchanging <c>_advertiseChannel</c> on every call —
/// is covered by the <see cref="ScriptedSeam"/> tests below, which enumerate through a
/// <see cref="ScriptedAdapter"/> whose start succeeds. That closed the gap this remark used to
/// track: the write-side tests here verify the write, the seam tests verify the swap.
/// </para>
/// </remarks>
[Trait("Category", "Connections")]
public class PlatformBridgeTests
{
    public sealed class WriteConnectionRequest : PlatformBridgeTests
    {
        [Fact]
        public async Task YieldsRequestOnAdvertiseChannel()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            NearbyConnectionRequest? captured = null;
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            var request = new NearbyConnectionRequest(
                device,
                accept: ct => tcs.Task.WaitAsync(ct),
                reject: ct => Task.CompletedTask);

            // Act
            platform.WriteConnectionRequest(request);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            // Read one item directly from the internal advertise channel reader
            var reader = platform._advertiseChannel.Reader;
            captured = await reader.ReadAsync(cts.Token);

            // Assert
            Assert.NotNull(captured);
            Assert.Same(device, captured.RemoteDevice);
        }

        [Fact]
        public async Task MultipleRequests_AllYielded()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device1 = Create.Device("peer-1", "Alice");
            var device2 = new NearbyDevice("peer-2", "Bob");

            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            platform.WriteConnectionRequest(new NearbyConnectionRequest(device1, ct => tcs.Task.WaitAsync(ct), ct => Task.CompletedTask));
            platform.WriteConnectionRequest(new NearbyConnectionRequest(device2, ct => tcs.Task.WaitAsync(ct), ct => Task.CompletedTask));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var reader = platform._advertiseChannel.Reader;

            // Act
            var first = await reader.ReadAsync(cts.Token);
            var second = await reader.ReadAsync(cts.Token);

            // Assert
            Assert.Equal("peer-1", first.RemoteDevice.Id);
            Assert.Equal("peer-2", second.RemoteDevice.Id);
        }
    }

    public sealed class ResolveConnectionTcs : PlatformBridgeTests
    {
        [Fact]
        public async Task AcceptAsync_ResolveConnectionTcs_CompletesWithNearbyConnection()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            // Simulate what ConnectAsync does: register a TCS keyed by peer ID
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            platform._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var connection = Create.Connection(device: device);

            // Act — simulate platform callback resolving the TCS
            platform.ResolveConnectionTcs("peer-1", connection);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var result = await tcs.Task.WaitAsync(cts.Token);

            // Assert
            Assert.Same(connection, result);
        }

        [Fact]
        public async Task RegistersConnectionInActiveConnections()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            platform._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var connection = Create.Connection(device: device);

            // Act
            platform.ResolveConnectionTcs("peer-1", connection);
            await tcs.Task; // wait for resolution

            // Assert — connection is now tracked in _activeConnections
            Assert.True(platform._activeConnections.ContainsKey("peer-1"));
        }

        [Fact]
        public async Task ExposesConnectionThroughTheSeam()
        {
            // The platform table is the one owner of "device X has a live connection" (C5); the
            // session queries these two members instead of keeping a second table.

            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            platform._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var connection = Create.Connection(device: device);

            // Act
            platform.ResolveConnectionTcs("peer-1", connection);
            await tcs.Task;

            // Assert
            Assert.True(platform.TryGetConnection("peer-1", out var lookedUp));
            Assert.Same(connection, lookedUp);
            Assert.Same(connection, Assert.Single(platform.SnapshotConnections()));
        }

        [Fact]
        public async Task FaultConnectionTcs_FaultsTheTcsWithGivenException()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            platform._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var expectedException = new InvalidOperationException("connection failed");

            // Act
            platform.FaultConnectionTcs("peer-1", expectedException);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tcs.Task);
        }

        [Fact]
        public void NoRegisteredTcs_SilentlyNoOps()
        {
            // Arrange - reproduces the iOS advertiser race window where a platform callback
            // (e.g. MCSessionState.Connected) can fire before the accept continuation
            // has registered its TCS in _connectionTcs. Callers must register the TCS before
            // triggering any platform operation that could resolve it; this test pins down
            // that ResolveConnectionTcs offers no rescue if that ordering is violated.
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            var connection = Create.Connection(device: device);

            // Act
            platform.ResolveConnectionTcs("peer-1", connection);

            // Assert - no TCS was registered, so the resolution is dropped and the connection
            // is never tracked as active; nothing throws.
            Assert.False(platform._activeConnections.ContainsKey("peer-1"));
        }
    }

    public sealed class WriteDeviceFound : PlatformBridgeTests
    {
        [Fact]
        public async Task YieldsFoundEventOnDiscoverChannel()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            // Act
            platform.WriteDeviceFound(device);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var evt = await platform._discoverChannel.Reader.ReadAsync(cts.Token);

            // Assert
            Assert.True(evt.Found);
            Assert.Same(device, evt.Device);
        }

        [Fact]
        public async Task WriteDeviceLost_YieldsLostEventOnDiscoverChannel()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            // Act
            platform.WriteDeviceLost(device);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var evt = await platform._discoverChannel.Reader.ReadAsync(cts.Token);

            // Assert
            Assert.False(evt.Found);
            Assert.Same(device, evt.Device);
        }

        [Fact]
        public async Task ThenLost_PreservesOrder()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            // Act
            platform.WriteDeviceFound(device);
            platform.WriteDeviceLost(device);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var reader = platform._discoverChannel.Reader;
            var first = await reader.ReadAsync(cts.Token);
            var second = await reader.ReadAsync(cts.Token);

            // Assert
            Assert.True(first.Found);
            Assert.False(second.Found);
        }
    }

    public sealed class WritePayload : PlatformBridgeTests
    {
        [Fact]
        public async Task RoutesPayloadToActiveConnection()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1", "Alice");

            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            platform._connectionTcs["peer-1"] = (tcs, CancellationToken.None);

            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = Create.Connection(device: device, receiveChannel: receiveChannel);

            platform.ResolveConnectionTcs("peer-1", connection);
            await tcs.Task;

            var payload = new NearbyBytesPayload([1, 2, 3]);

            // Act
            platform.WritePayload("peer-1", payload);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var received = await receiveChannel.Reader.ReadAsync(cts.Token);

            // Assert
            Assert.Same(payload, received);
        }

        [Fact]
        public void UnknownPeer_DoesNotThrow()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            var payload = new NearbyBytesPayload([1, 2, 3]);

            // Act
            platform.WritePayload("nonexistent-peer", payload);

            // Assert — unknown peer silently ignored; no connection was registered
            Assert.False(platform._activeConnections.ContainsKey("nonexistent-peer"));
        }
    }

    public sealed class DisposeAsync : PlatformBridgeTests
    {
        [Fact]
        public async Task CompletesAdvertiseChannel()
        {
            // Arrange
            var platform = Create.PlatformBridge();

            // Act
            await platform.DisposeAsync();

            // Assert — channel writer is completed, so reader will complete immediately
            Assert.True(platform._advertiseChannel.Reader.Completion.IsCompleted);
        }

        [Fact]
        public async Task CompletesDiscoverChannel()
        {
            // Arrange
            var platform = Create.PlatformBridge();

            // Act
            await platform.DisposeAsync();

            // Assert
            Assert.True(platform._discoverChannel.Reader.Completion.IsCompleted);
        }

        [Fact]
        public async Task CancelsPendingConnectionTcs()
        {
            // Arrange
            var platform = Create.PlatformBridge();
            using var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            platform._connectionTcs["peer-1"] = (tcs, cts.Token);

            // Act
            await platform.DisposeAsync();

            // Assert
            Assert.True(tcs.Task.IsCanceled);
        }

        [Fact]
        public async Task CalledTwice_DoesNotThrow()
        {
            // Arrange
            var platform = Create.PlatformBridge();

            // Act
            await platform.DisposeAsync();
#pragma warning disable S3966 // intentional: second call verifies idempotency
            await platform.DisposeAsync();
#pragma warning restore S3966

            // Assert
            Assert.True(platform._advertiseChannel.Reader.Completion.IsCompleted);
        }
    }

    /// <summary>
    /// The logic only the scripted adapter can reach off-device: the channel swap on restart, the
    /// teardown-versus-deadline attribution, and the drain-then-release order (contract C7).
    /// </summary>
    public sealed class ScriptedSeam : PlatformBridgeTests
    {
        [Fact]
        public async Task Restart_SwapsToAFreshChannel_AndTheNewEnumerationReceives()
        {
            // Arrange — an adapter whose start succeeds, so enumeration reaches the channel.
            var adapter = new ScriptedAdapter { OnStartAdvertising = static _ => Task.CompletedTask };
            var platform = Create.PlatformBridge(adapter: adapter);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act — first session receives, ends, and a second session receives on the fresh channel.
            var first = platform
                .AdvertiseAsync(Create.Gate(), cts.Token)
                .GetAsyncEnumerator(cts.Token);
            var firstMove = first.MoveNextAsync();
            platform.WriteConnectionRequest(Create.Request(Create.Device("peer-1")));
            Assert.True(await firstMove);
            var firstRequest = first.Current;
            await first.DisposeAsync();

            var second = platform
                .AdvertiseAsync(Create.Gate(), cts.Token)
                .GetAsyncEnumerator(cts.Token);
            var secondMove = second.MoveNextAsync();
            platform.WriteConnectionRequest(Create.Request(Create.Device("peer-2")));
            Assert.True(await secondMove);
            var secondRequest = second.Current;
            await second.DisposeAsync();

            // Assert — the restart reached a channel that was not the completed first one.
            Assert.Equal("peer-1", firstRequest.RemoteDevice.Id);
            Assert.Equal("peer-2", secondRequest.RemoteDevice.Id);
        }

        [Fact]
        public async Task DisposeMidHandshake_CancelsThePendingHandshake()
        {
            // The teardown source of cancellation, attributed as cancellation — not reported as a
            // deadline that never elapsed. The device suite pins this on-device; this is the
            // off-device twin the scripted seam makes possible.

            // Arrange
            var platform = Create.PlatformBridge();
            var device = Create.Device("peer-1");
            var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);
            var pending = platform.AwaitHandshakeAsync(
                device,
                tcs,
                ConnectionRole.Acceptor,
                beforeAwait: static _ => Task.CompletedTask,
                CancellationToken.None);

            // Act
            await platform.DisposeAsync();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }

        [Fact]
        public async Task Release_DrainsThePeersWorkBeforeTheAdapterReleases()
        {
            // Drain, then release (C7): the adapter must not free the peer's handles while queued
            // inbound work can still be reading them.

            // Arrange — a queued task the test holds open.
            var adapter = new ScriptedAdapter();
            var platform = Create.PlatformBridge(adapter: adapter);
            var gate = Create.Gate();
            _ = platform.WorkQueue.Enqueue("peer-1", () => gate.Task);
            await Task.Yield();

            // Act
            var release = platform.ReleaseConnectionAsync("peer-1").AsTask();
            await Task.Delay(50, TestContext.Current.CancellationToken);
            var releasedWhileDraining = adapter.Released.Count;
            gate.SetResult();
            await release;

            // Assert
            Assert.Equal(0, releasedWhileDraining);
            Assert.Equal("peer-1", Assert.Single(adapter.Released));
        }
    }
}
