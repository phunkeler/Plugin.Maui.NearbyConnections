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
/// The cost is real and worth stating: <c>AdvertiseAsync</c> swaps <c>_advertiseChannel</c> via
/// <c>Interlocked.Exchange</c> on every call, so these tests pass partly because nothing enumerates
/// during them. They verify the write side, not the swap. Closing this properly means giving the
/// <c>net10.0</c> target a way to enumerate without a platform start — tracked as open
/// question 7 in docs/DEVICE-LIFECYCLE.md, because it changes what the platform-support stub does
/// rather than how a test is written.
/// </para>
/// </remarks>
[Trait("Category", "Connections")]
public class PlatformNearbyTests
{
    public sealed class WriteConnectionRequest : PlatformNearbyTests
    {
        [Fact]
        public async Task YieldsRequestOnAdvertiseChannel()
        {
            // Arrange
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
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

    public sealed class ResolveConnectionTcs : PlatformNearbyTests
    {
        [Fact]
        public async Task AcceptAsync_ResolveConnectionTcs_CompletesWithNearbyConnection()
        {
            // Arrange
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
            var device = Create.Device("peer-1", "Alice");

            var connection = Create.Connection(device: device);

            // Act
            platform.ResolveConnectionTcs("peer-1", connection);

            // Assert - no TCS was registered, so the resolution is dropped and the connection
            // is never tracked as active; nothing throws.
            Assert.False(platform._activeConnections.ContainsKey("peer-1"));
        }
    }

    public sealed class WriteDeviceFound : PlatformNearbyTests
    {
        [Fact]
        public async Task YieldsFoundEventOnDiscoverChannel()
        {
            // Arrange
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
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

    public sealed class WritePayload : PlatformNearbyTests
    {
        [Fact]
        public async Task RoutesPayloadToActiveConnection()
        {
            // Arrange
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();
            var payload = new NearbyBytesPayload([1, 2, 3]);

            // Act
            platform.WritePayload("nonexistent-peer", payload);

            // Assert — unknown peer silently ignored; no connection was registered
            Assert.False(platform._activeConnections.ContainsKey("nonexistent-peer"));
        }
    }

    public sealed class DisposeAsync : PlatformNearbyTests
    {
        [Fact]
        public async Task CompletesAdvertiseChannel()
        {
            // Arrange
            var platform = Create.PlatformNearby();

            // Act
            await platform.DisposeAsync();

            // Assert — channel writer is completed, so reader will complete immediately
            Assert.True(platform._advertiseChannel.Reader.Completion.IsCompleted);
        }

        [Fact]
        public async Task CompletesDiscoverChannel()
        {
            // Arrange
            var platform = Create.PlatformNearby();

            // Act
            await platform.DisposeAsync();

            // Assert
            Assert.True(platform._discoverChannel.Reader.Completion.IsCompleted);
        }

        [Fact]
        public async Task CancelsPendingConnectionTcs()
        {
            // Arrange
            var platform = Create.PlatformNearby();
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
            var platform = Create.PlatformNearby();

            // Act
            await platform.DisposeAsync();
#pragma warning disable S3966 // intentional: second call verifies idempotency
            await platform.DisposeAsync();
#pragma warning restore S3966

            // Assert
            Assert.True(platform._advertiseChannel.Reader.Completion.IsCompleted);
        }
    }
}
