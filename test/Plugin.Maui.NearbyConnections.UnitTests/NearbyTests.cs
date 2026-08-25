using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Behavioural tests for <see cref="Nearby"/>.
/// </summary>
/// <remarks>
/// The session takes <see cref="IPlatformNearby"/> rather than the concrete implementation
/// precisely so these can run on <c>net10.0</c>, where every <c>Platform*</c> start throws.
/// </remarks>
[Trait("Category", "Session")]
public class NearbyTests
{
    // -------------------------------------------------------------------------
    // Preflight availability
    // -------------------------------------------------------------------------

    public sealed class CheckAvailability : NearbyTests
    {
        [Fact]
        public async Task Always_DelegatesToThePlatform()
        {
            // Arrange
            var connections = new FakeNearby { Availability = NearbyAvailability.Ready };
            var session = Create.Session(connections);

            // Act
            var result = await session.CheckAvailabilityAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(NearbyAvailability.Ready, result);
            Assert.Equal(1, connections.CheckAvailabilityCallCount);
        }

        [Fact]
        public async Task MultipleProblems_AreReportedTogether()
        {
            // The whole reason this is a [Flags] enum: a user with Bluetooth off AND permissions
            // denied should be told both at once, not made to fix one and retry to discover the
            // other.

            // Arrange
            var connections = new FakeNearby
            {
                Availability = NearbyAvailability.BluetoothDisabled | NearbyAvailability.MissingPermissions,
            };
            var session = Create.Session(connections);

            // Act
            var result = await session.CheckAvailabilityAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasFlag(NearbyAvailability.BluetoothDisabled));
            Assert.True(result.HasFlag(NearbyAvailability.MissingPermissions));
            Assert.False(result.HasFlag(NearbyAvailability.PlayServicesUnavailable));
        }

        [Fact]
        public async Task ProblemFlags_DoNotCompareEqualToReady()
        {
            // Ready = 0 is what makes `result is NearbyAvailability.Ready` a valid readiness test.
            // If Ready ever gained a non-zero value, or a problem flag were assigned 0, that idiom
            // would silently start reporting a broken device as usable.

            // Arrange
            var connections = new FakeNearby
            {
                Availability = NearbyAvailability.MissingPermissions,
            };
            var session = Create.Session(connections);

            // Act
            var result = await session.CheckAvailabilityAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEqual(NearbyAvailability.Ready, result);
        }

        [Fact]
        public async Task DoesNotStartAdvertisingOrDiscovery()
        {
            // A preflight check must not have side effects: it reports state, it does not mutate it.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.CheckAvailabilityAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(session.IsAdvertising);
            Assert.False(session.IsDiscovering);
            Assert.Equal(0, connections.AdvertiseCallCount);
            Assert.Equal(0, connections.DiscoverCallCount);
        }

        [Fact]
        public async Task CanceledToken_Throws()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => session.CheckAvailabilityAsync(cts.Token));
        }
    }

    // -------------------------------------------------------------------------
    // Advertising / discovery toggles — decision 8: they are independent.
    // -------------------------------------------------------------------------

    public sealed class Toggles : NearbyTests
    {
        [Fact]
        public async Task StartAdvertisingAsync_SetsIsAdvertising_WithoutSettingIsDiscovering()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(session.IsAdvertising);
            Assert.False(session.IsDiscovering, "Advertising must not imply discovering — both sample pages toggle them separately.");
        }

        [Fact]
        public async Task StartDiscoveryAsync_SetsIsDiscovering_WithoutSettingIsAdvertising()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(session.IsDiscovering);
            Assert.False(session.IsAdvertising);
        }

        [Fact]
        public async Task StopAdvertisingAsync_ClearsIsAdvertising_AndLeavesDiscoveryRunning()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Act
            await session.StopAdvertisingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(session.IsAdvertising);
            Assert.True(session.IsDiscovering, "Stopping one must not stop the other.");
        }

        [Fact]
        public async Task StopAsync_ClearsBothToggles()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Act
            await session.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(session.IsAdvertising);
            Assert.False(session.IsDiscovering);
        }

        [Fact]
        public async Task StartAdvertisingAsync_CalledTwice_IsNoOp()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(session.IsAdvertising);
            // A second start must not reach the platform again.
            Assert.Equal(1, connections.AdvertiseCallCount);
        }
    }

    // -------------------------------------------------------------------------
    // Start failures. A platform start failure now faults the Task StartAdvertisingAsync/
    // StartDiscoveryAsync returns, matching the documented INearby contract. A fault that arrives
    // after a successful start is a separate, later failure and is still only observable through
    // IsAdvertising/IsDiscovering flipping false.
    // -------------------------------------------------------------------------

    public sealed class StartFailures : NearbyTests
    {
        [Fact]
        public async Task AdvertiseStartFailure_ThrowsAndClearsIsAdvertising()
        {
            // Arrange
            var connections = new FakeNearby
            {
                AdvertiseFault = new NearbyAdvertisingException("radio off"),
            };
            var session = Create.Session(connections);

            // Act & Assert
            await Assert.ThrowsAsync<NearbyAdvertisingException>(
                () => session.StartAdvertisingAsync(TestContext.Current.CancellationToken));

            Assert.False(session.IsAdvertising, "A failed start must not leave the session claiming to advertise.");
        }

        [Fact]
        public async Task DiscoverStartFailure_ThrowsAndClearsIsDiscovering()
        {
            // Arrange
            var connections = new FakeNearby
            {
                DiscoverFault = new NearbyDiscoveryException("permission denied"),
            };
            var session = Create.Session(connections);

            // Act & Assert
            await Assert.ThrowsAsync<NearbyDiscoveryException>(
                () => session.StartDiscoveryAsync(TestContext.Current.CancellationToken));

            Assert.False(session.IsDiscovering);
        }

        [Fact]
        public async Task AdvertiseStartFailure_IsRetryable()
        {
            // A failed start must clean up the dead pump so a retry reaches the platform again,
            // rather than reusing a Task/CancellationTokenSource pair that already faulted.

            // Arrange
            var connections = new FakeNearby
            {
                AdvertiseFault = new NearbyAdvertisingException("radio off"),
            };
            var session = Create.Session(connections);

            // Act
            await Assert.ThrowsAsync<NearbyAdvertisingException>(
                () => session.StartAdvertisingAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<NearbyAdvertisingException>(
                () => session.StartAdvertisingAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal(2, connections.AdvertiseCallCount);
        }

        [Fact]
        public async Task AdvertiseFaultAfterSuccessfulStart_DoesNotThrowFromStartAdvertisingAsync()
        {
            // A late fault (e.g. the platform's radio drops mid-session) is a different failure
            // mode from a start failure: it must not retroactively fault the already-returned
            // StartAdvertisingAsync Task. ChangeStreams covers the AdvertisingChanges signal.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            // Act
            connections.FaultAdvertiseStream(new NearbyAdvertisingException("radio dropped"));
            await Wait.UntilAsync(() => !session.IsAdvertising);

            // Assert
            Assert.False(session.IsAdvertising);
        }

        [Fact]
        public async Task DiscoverFaultAfterSuccessfulStart_DoesNotThrowFromStartDiscoveryAsync()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Act
            connections.FaultDiscoverStream(new NearbyDiscoveryException("radio dropped"));
            await Wait.UntilAsync(() => !session.IsDiscovering);

            // Assert
            Assert.False(session.IsDiscovering);
        }

        [Fact]
        public async Task AdvertiseFaultAfterSuccessfulStart_PumpTakesLoggedPathAndSessionRestarts()
        {
            // The dead pump's own Cts/Task must not wedge a later restart: a fault that loses the
            // started race (started.TrySetException returns false, so the pump falls through to the
            // logged path instead of rethrowing) still leaves the pump cleanly stoppable, so a fresh
            // StartAdvertisingAsync reaches the platform again rather than hanging on stale state.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            connections.FaultAdvertiseStream(new NearbyAdvertisingException("radio dropped"));
            await Wait.UntilAsync(() => !session.IsAdvertising);

            // Act
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(session.IsAdvertising);
            Assert.Equal(2, connections.AdvertiseCallCount);
        }

        [Fact]
        public async Task DiscoverFaultAfterSuccessfulStart_PumpTakesLoggedPathAndSessionRestarts()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);
            connections.FaultDiscoverStream(new NearbyDiscoveryException("radio dropped"));
            await Wait.UntilAsync(() => !session.IsDiscovering);

            // Act
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(session.IsDiscovering);
            Assert.Equal(2, connections.DiscoverCallCount);
        }
    }

    // -------------------------------------------------------------------------
    // Discovery projected onto the Devices collection.
    // -------------------------------------------------------------------------

    public sealed class Discovery : NearbyTests
    {
        [Fact]
        public async Task DeviceFound_AddsToDevices()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            // Assert
            Assert.Single(session.Devices);
            Assert.Same(device, session.Devices[0]);
            Assert.Equal(NearbyDeviceStatus.Visible, device.Status);
        }

        [Fact]
        public async Task DeviceFound_Twice_DoesNotDuplicate()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            await connections.EmitDeviceFoundAsync(device);

            // Assert
            Assert.Single(session.Devices);
        }

        [Fact]
        public async Task DeviceLost_RemovesVisibleDevice()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            await connections.EmitDeviceLostAsync(device);

            // Assert
            Assert.Empty(session.Devices);
        }

        [Fact]
        public async Task DeviceLost_DoesNotRemoveConnectedDevice()
        {
            // Going out of discovery range is not the same as disconnecting. Removing a connected
            // device here would delete a live conversation from the UI.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            // Act
            await connections.EmitDeviceLostAsync(device);

            // Assert
            Assert.Single(session.Devices);
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
        }

        [Fact]
        public async Task StopDiscoveryAsync_DrainsVisibleDevices()
        {
            // Otherwise the UI shows devices that are no longer being looked for, forever.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-1", "Alice"));
            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-2", "Bob"));

            // Act
            await session.StopDiscoveryAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(session.Devices);
        }

        [Fact]
        public async Task StopDiscoveryAsync_KeepsConnectedDevices()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            // Act
            await session.StopDiscoveryAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(session.Devices);
        }
    }

    // -------------------------------------------------------------------------
    // Inbound requests: accept / reject.
    // -------------------------------------------------------------------------

    public sealed class InboundRequests : NearbyTests
    {
        [Fact]
        public async Task RequestArriving_ReportsRequestReceived_AndSurfacesDevice()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);

            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await recorder.WaitForAsync("peer-1", 2);

            // Assert
            Assert.Equal(NearbyDeviceStatus.RequestReceived, session.StatusOf("peer-1"));
            Assert.Contains(device, session.Devices);

            // Added before Updated: a consumer must never see a status change for a device it has
            // not been told about.
            Assert.Equal(
                new[] { NearbyDeviceChangeAction.Added, NearbyDeviceChangeAction.Updated },
                recorder.For("peer-1").Select(c => c.Action).ToArray());
        }

        [Fact]
        public async Task AutoAccept_ConnectsWithoutEverReportingRequestReceived()
        {
            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await recorder.WaitForAsync("peer-1", 2);

            // Assert — the documented contract of auto-accept: the state is skipped, not merely
            // unreported.
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
            // RequestReceived must not be observable when requests are auto-accepted.
                Assert.DoesNotContain(
                    NearbyDeviceStatus.RequestReceived,
                    recorder.StatusesFor("peer-1"));
        }

        [Fact]
        public async Task AutoAccept_WhenTheAcceptNeverSettles_DisposalCancelsIt()
        {
            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            var accept = connections.CaptureNextAcceptToken();
            await connections.EmitRequestThatOnlyCancellationEndsAsync(new NearbyDevice("peer-1", "Alice"));

            // Act
            await session.DisposeAsync();

            // Assert — auto-accept is started by a callback, so no caller's token reaches it. The
            // session passes its own disposal token instead, which is what ends an accept the
            // platform never settles. Nothing awaits auto-accept today, so this is the only
            // observable effect; it is also what keeps a future disposal drain from hanging here.
            await Wait.UntilAsync(() => accept.Task.IsCompleted);
            Assert.True(
                accept.Task.IsCanceled,
                "Disposal must cancel a pending auto-accept, not leave it awaiting forever.");
        }

        [Fact]
        public async Task AutoAccept_LeavesNoPendingRequestToAnswer()
        {
            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await connections.EmitRequestAsync(device, () => Create.Connection(device));

            // The session already answered, so there is nothing left for the application to accept.

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.AcceptAsync(device, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AutoAccept_WhenAcceptFails_ResetsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await connections.EmitRequestAsync(
                device,
                () => throw new NearbyException("Handshake failed."));

            // A failed auto-accept must not strand the row on Connecting, and must not escape into
            // the advertise pump and stop advertising.

            // Assert
            Assert.Equal(NearbyDeviceStatus.Visible, device.Status);
            Assert.True(session.IsAdvertising);
        }

        [Fact]
        public async Task StopAsync_CancelsAPendingAutoAccept_SoItCannotWriteIntoTheNextSession()
        {
            // Stop promises a return to the initial state. An auto-accept that survived a stop
            // could resolve later and resurrect a registry row (section 3, decided item 1).

            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            var device = new NearbyDevice("peer-1", "Alice");
            var accept = connections.CaptureNextAcceptToken();
            await connections.EmitRequestThatOnlyCancellationEndsAsync(device);

            // Act — stop, then try to complete the old accept as a straggler would.
            await session.StopAsync(TestContext.Current.CancellationToken);
            var resurrected = accept.TrySetResult(Create.Connection(device));
            await Task.Yield();

            // Assert — the stop token already settled the accept, so the straggler cannot land.
            Assert.False(resurrected);
            Assert.Empty(session.Devices);
        }

        [Fact]
        public async Task UnansweredRequest_WhenTimeoutElapses_RejectsAndReturnsDeviceToVisible()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var timeout = TimeSpan.FromSeconds(30);
            var connections = new FakeNearby();
            var session = Create.Session(connections, new NearbyOptions { InboundRequestTimeout = timeout }, time);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");
            var rejected = false;

            await connections.EmitRequestAsync(device, () => Create.Connection(device), onReject: () => rejected = true);
            await recorder.WaitForAsync("peer-1", 2);

            // Act
            time.Advance(timeout);
            await recorder.WaitForAsync("peer-1", 3);

            // Assert
            Assert.True(rejected, "An expired request must be rejected, to release the platform's handle.");
            Assert.Equal(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
        }

        [Fact]
        public async Task ExpiredRequest_CannotBeAccepted()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var timeout = TimeSpan.FromSeconds(30);
            var connections = new FakeNearby();
            var session = Create.Session(connections, new NearbyOptions { InboundRequestTimeout = timeout }, time);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await recorder.WaitForAsync("peer-1", 2);

            // Act
            time.Advance(timeout);
            await recorder.WaitForAsync("peer-1", 3);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.AcceptAsync(device, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task PendingRequest_PublishesExpiryForAConsumerCountdown()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var timeout = TimeSpan.FromSeconds(30);
            var connections = new FakeNearby();
            var session = Create.Session(connections, new NearbyOptions { InboundRequestTimeout = timeout }, time);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");
            var expected = time.GetUtcNow() + timeout;

            // Act
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await recorder.WaitForAsync("peer-1", 2);

            // Assert
            Assert.Equal(expected, session.Current("peer-1")?.RequestExpiresAt);
        }

        [Fact]
        public async Task AcceptedRequest_ClearsTheExpiry()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var connections = new FakeNearby();
            var session = Create.Session(connections, new NearbyOptions { InboundRequestTimeout = TimeSpan.FromSeconds(30) }, time);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await recorder.WaitForAsync("peer-1", 2);

            // Act
            await session.AcceptAsync(device, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(session.Current("peer-1")?.RequestExpiresAt);
        }

        [Fact]
        public async Task AcceptedRequest_IsNotRejectedWhenTheOriginalTimeoutWouldHaveElapsed()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var timeout = TimeSpan.FromSeconds(30);
            var connections = new FakeNearby();
            var session = Create.Session(connections, new NearbyOptions { InboundRequestTimeout = timeout }, time);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");
            var rejected = false;

            await connections.EmitRequestAsync(device, () => Create.Connection(device), onReject: () => rejected = true);
            await recorder.WaitForAsync("peer-1", 2);
            await session.AcceptAsync(device, TestContext.Current.CancellationToken);

            // Act
            time.Advance(timeout * 2);

            // Assert
            Assert.False(rejected, "A disarmed countdown must not reject an accepted request.");
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
        }

        [Fact]
        public async Task InfiniteTimeout_LeavesTheRequestOutstanding()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var connections = new FakeNearby();
            var session = Create.Session(connections, new NearbyOptions { InboundRequestTimeout = Timeout.InfiniteTimeSpan }, time);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");
            var rejected = false;

            await connections.EmitRequestAsync(device, () => Create.Connection(device), onReject: () => rejected = true);
            await recorder.WaitForAsync("peer-1", 2);

            // Act
            time.Advance(TimeSpan.FromHours(1));

            // Assert
            Assert.False(rejected);
            Assert.Equal(NearbyDeviceStatus.RequestReceived, session.StatusOf("peer-1"));
            // A request that does not expire must not publish an expiry instant.
            Assert.Null(session.Current("peer-1")?.RequestExpiresAt);
        }

        [Fact]
        public async Task AcceptAsync_ConnectsAndReportsConnected()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            var connection = Create.Connection(device);
            await connections.EmitRequestAsync(device, () => connection);

            await using var recorder = new ChangeRecorder(session);

            // Act
            var result = await session.AcceptAsync(device, TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(connection, result);
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
            Assert.Equal(ConnectionRole.Acceptor, session.Current("peer-1")!.Role);
            Assert.True(session.TryGetConnection(device.Id, out var lookedUp));
            Assert.Same(connection, lookedUp);

            // Wait for the Connected change specifically. A plain count reaches 1 as soon as any
            // change lands, which may be an earlier transition in the handshake.
            await Wait.UntilAsync(
                () => recorder.StatusesFor("peer-1").Contains(NearbyDeviceStatus.Connected));

            Assert.Contains(NearbyDeviceStatus.Connected, recorder.StatusesFor("peer-1"));
        }

        [Fact]
        public async Task RejectAsync_DoesNotConnect()
        {
            // Security-relevant: rejecting must never produce a connection.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => Create.Connection(device));

            await using var recorder = new ChangeRecorder(session);

            // Act
            await session.RejectAsync(device, TestContext.Current.CancellationToken);

            // Assert
            Assert.DoesNotContain(NearbyDeviceStatus.Connected, recorder.StatusesFor("peer-1"));
            Assert.False(session.TryGetConnection(device.Id, out _));
            Assert.Equal(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
        }

        [Fact]
        public async Task AcceptAsync_AfterReject_Throws()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await session.RejectAsync(device, TestContext.Current.CancellationToken);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.AcceptAsync(device, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AcceptAsync_WithNoOutstandingRequest_Throws()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.AcceptAsync(new NearbyDevice("peer-1", "Alice"), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AcceptAsync_WhenPlatformFails_ResetsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(
                device,
                () => throw new InvalidOperationException("handshake failed"));

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.AcceptAsync(device, TestContext.Current.CancellationToken));

            // Assert
            // A failed handshake must not strand the row on Connecting.
            Assert.Equal(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
        }
    }

    // -------------------------------------------------------------------------
    // Outbound connect.
    // -------------------------------------------------------------------------

    public sealed class Connect : NearbyTests
    {
        [Fact]
        public async Task ConnectAsync_SetsConnectedStateAndReportsIt()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = Create.Connection(device);
            connections.ConnectResult = connection;

            await using var recorder = new ChangeRecorder(session);

            // Act
            var result = await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(connection, result);
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
            Assert.Equal(ConnectionRole.Initiator, session.Current("peer-1")!.Role);
            Assert.True(session.TryGetConnection(device.Id, out _));

            await Wait.UntilAsync(
                () => recorder.StatusesFor("peer-1").Contains(NearbyDeviceStatus.Connected));

            // Reaching Connected must be reported as a change, not only reflected in Devices.
                Assert.Contains(
                    NearbyDeviceStatus.Connected,
                    recorder.StatusesFor("peer-1"));
        }

        [Fact]
        public async Task ConnectAsync_WhenRejected_ResetsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby
            {
                ConnectFault = new InvalidOperationException("rejected"),
            };
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConnectAsync(device, TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal(NearbyDeviceStatus.Visible, device.Status);
        }

        [Fact]
        public async Task ConnectAsync_NullDevice_Throws()
        {
            // Arrange
            var session = Create.Session(new FakeNearby());

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => session.ConnectAsync(null!, TestContext.Current.CancellationToken));
        }
    }

    // -------------------------------------------------------------------------
    // Disconnect. Guards P2-3: the drop must be reported exactly once, whichever
    // side ended it.
    // -------------------------------------------------------------------------

    public sealed class Disconnect : NearbyTests
    {
        [Fact]
        public async Task RemoteDisconnect_RaisesConnectionDroppedExactlyOnce()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = Create.Connection(device);
            connections.ConnectResult = connection;

            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);

            // Act
            await connection.DisposeAsync();

            // The registry is written before subscribers are published to, so StatusOf can already
            // read Visible while the recorder's channel is still undrained. Wait on the recorder.
            await Wait.UntilAsync(() => recorder.StatusesFor("peer-1").Any());

            // Assert
            // A duplicate drop was a previously fixed bug (P2-3).
            Assert.Single(recorder.StatusesFor("peer-1"));
            Assert.Equal(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
        }

        [Fact]
        public async Task DisposeAsync_AfterADisconnect_LeavesTheRegistryEmpty()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = Create.Connection(device);
            connections.ConnectResult = connection;

            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            // Act — StopAsync disposes each live connection, and the platform's own table clears
            // on release, so the disconnect watcher wakes mid-disposal and calls ResetToVisible
            // after _registry.Clear() has already run.
            await session.DisposeAsync();

            // Give the watcher every chance to run and re-add the row.
            await Wait.UntilAsync(() => session.Devices.Count > 0);

            // Assert
            // A watcher waking after disposal must not resurrect a row. Registry.Update returns
            // early for an absent id, which is what makes this hold — keep it that way.
            Assert.Empty(session.Devices);
        }

        [Fact]
        public async Task DisconnectAsync_ReportsTheDropExactlyOnce()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);

            // Act
            await session.DisconnectAsync(device, TestContext.Current.CancellationToken);

            // The registry is written before subscribers are published to, so StatusOf can already
            // read Visible while the recorder's channel is still undrained. Wait on the recorder.
            await Wait.UntilAsync(() => recorder.StatusesFor("peer-1").Any());

            // Assert
            // A local disconnect must be reported exactly like a remote one.
            Assert.Single(recorder.StatusesFor("peer-1"));
        }

        [Fact]
        public async Task DisconnectAsync_WhenNotConnected_IsNoOp()
        {
            // Arrange
            var session = Create.Session(new FakeNearby());
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await session.DisconnectAsync(device, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(NearbyDeviceStatus.Visible, device.Status);
        }

        // GAP: the reason a connection ended is no longer observable by consumers. It travelled on
        // NearbyConnectionChangedEventArgs.Reason, and a device back in Visible carries no reason.
        // EndReason now reaches logs only. This test asserts what a consumer CAN still see, and
        // exists to be rewritten when a reason is reattached to the transition.
        [Fact]
        public async Task ReturnsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            // Act
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await session.DisconnectAsync(device, TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => session.StatusOf("peer-1") is NearbyDeviceStatus.Visible);

            // Assert
            Assert.Equal(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
            // A disconnected device plays no role.
            Assert.Null(session.Current("peer-1")!.Role);
        }

        // DiscoveryPageViewModel filters on "Visible or Connecting". A device left in any other
        // state after a drop silently vanishes from the discovery list — a capability regression
        // that no other test in this suite would catch.
        [Fact]
        public async Task DroppedDevice_RejoinsTheDiscoveryFilter()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            // Act
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await session.DisconnectAsync(device, TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => session.StatusOf("peer-1") is not NearbyDeviceStatus.Connected);

            // Assert
            Assert.True(
                session.StatusOf("peer-1") is NearbyDeviceStatus.Visible or NearbyDeviceStatus.Connecting,
                $"A dropped device must rejoin the discovery filter, but was {session.StatusOf("peer-1")}.");
        }

        // Cancelled, TimedOut and Failed are branched per exception type rather than defaulted, so
        // a caller that withdraws is not reported as a failure.
        [Fact]
        public async Task ConnectAsync_Cancelled_LeavesDeviceVisible()
        {
            // Arrange
            var connections = new FakeNearby { ConnectFault = new OperationCanceledException() };
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await Assert.ThrowsAsync<OperationCanceledException>(() => session.ConnectAsync(device, TestContext.Current.CancellationToken));

            // Assert
            // A cancelled handshake must not strand the row on Connecting.
            Assert.Equal(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
            // A device that is not connecting carries no role.
            Assert.Null(session.Current("peer-1")!.Role);
        }

        [Fact]
        public async Task DisconnectAsync_LeavesOtherConnectionsIntact()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            var alice = new NearbyDevice("peer-1", "Alice");
            var bob = new NearbyDevice("peer-2", "Bob");

            connections.ConnectResult = Create.Connection(alice);
            await session.ConnectAsync(alice, TestContext.Current.CancellationToken);
            connections.ConnectResult = Create.Connection(bob);
            await session.ConnectAsync(bob, TestContext.Current.CancellationToken);

            await session.DisconnectAsync(alice, TestContext.Current.CancellationToken);

            // Alice's entry is removed on the Disconnected continuation, not inside DisconnectAsync
            // — the same asynchrony documented by StopAsync_ClearingDeviceState_IsNotSynchronous.

            // Act
            await Wait.UntilAsync(() => !session.TryGetConnection(alice.Id, out _));

            // Assert
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-2"));
            Assert.True(session.TryGetConnection(bob.Id, out _), "Disconnecting one device must not tear down the others.");
        }
    }

    // -------------------------------------------------------------------------
    // Payload delivery — the stream survives untouched by this restructure.
    // -------------------------------------------------------------------------

    public sealed class Payloads : NearbyTests
    {
        [Fact]
        public async Task PayloadsFromMultipleConnections_AllArrive()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            var alice = new NearbyDevice("peer-1", "Alice");
            var bob = new NearbyDevice("peer-2", "Bob");

            var aliceChannel = Channel.CreateUnbounded<NearbyPayload>();
            var bobChannel = Channel.CreateUnbounded<NearbyPayload>();

            connections.ConnectResult = Create.Connection(alice, aliceChannel);
            var aliceConnection = await session.ConnectAsync(alice, TestContext.Current.CancellationToken);
            connections.ConnectResult = Create.Connection(bob, bobChannel);
            var bobConnection = await session.ConnectAsync(bob, TestContext.Current.CancellationToken);

            aliceChannel.Writer.TryWrite(new NearbyBytesPayload([1]));
            bobChannel.Writer.TryWrite(new NearbyBytesPayload([2]));
            aliceChannel.Writer.TryComplete();
            bobChannel.Writer.TryComplete();

            var aliceCount = 0;
            var bobCount = 0;

            // Act
            await foreach (var _ in aliceConnection.ReceiveAsync(TestContext.Current.CancellationToken))
            {
                aliceCount++;
            }

            await foreach (var _ in bobConnection.ReceiveAsync(TestContext.Current.CancellationToken))
            {
                bobCount++;
            }

            // Assert
            Assert.Equal(1, aliceCount);
            Assert.Equal(1, bobCount);
        }
    }

    // -------------------------------------------------------------------------
    // Hazards flagged by the test-mining pass.
    // -------------------------------------------------------------------------

    public sealed class ChangeStreams : NearbyTests
    {
        [Fact]
        public async Task StartAdvertisingAsync_PublishesTrue()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await using var changes = session.AdvertisingChanges.GetAsyncEnumerator(TestContext.Current.CancellationToken);

            // Act
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(await changes.MoveNextAsync());
            Assert.True(changes.Current);
        }

        [Fact]
        public async Task StopAdvertisingAsync_PublishesFalse()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await using var changes = session.AdvertisingChanges.GetAsyncEnumerator(TestContext.Current.CancellationToken);

            // Act
            await session.StopAdvertisingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(await changes.MoveNextAsync());
            Assert.False(changes.Current);
        }

        [Fact]
        public async Task StopAdvertisingAsync_WhenNotAdvertising_PublishesNothing()
        {
            // A stop that stops nothing is not a transition. The channel is FIFO, so
            // a later real transition arriving as the first item proves nothing preceded it.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await using var changes = session.AdvertisingChanges.GetAsyncEnumerator(TestContext.Current.CancellationToken);

            // Act
            await session.StopAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(await changes.MoveNextAsync());
            Assert.True(changes.Current, "The no-op stop must not have published false ahead of the real start.");
        }

        [Fact]
        public async Task AdvertiseStartFailure_PublishesTrueThenFalse()
        {
            // The flag is set before the platform confirms the start, so a failed start is a real
            // pair of transitions. The stream publishes exactly what the property reports.

            // Arrange
            var connections = new FakeNearby { AdvertiseFault = new NearbyAdvertisingException("nope") };
            var session = Create.Session(connections);
            await using var changes = session.AdvertisingChanges.GetAsyncEnumerator(TestContext.Current.CancellationToken);

            // Act
            await Assert.ThrowsAsync<NearbyAdvertisingException>(
                () => session.StartAdvertisingAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.True(await changes.MoveNextAsync());
            Assert.True(changes.Current);
            Assert.True(await changes.MoveNextAsync());
            Assert.False(changes.Current);
        }

        [Fact]
        public async Task AdvertiseFaultAfterSuccessfulStart_PublishesFalse_AndLeavesDiscoveryRunning()
        {
            // The finding-17 scenario: advertising dies mid-session with no caller involved.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            await using var advertising = session.AdvertisingChanges.GetAsyncEnumerator(TestContext.Current.CancellationToken);

            // Act
            connections.FaultAdvertiseStream(new NearbyAdvertisingException("radio dropped"));

            // Assert
            Assert.True(await advertising.MoveNextAsync());
            Assert.False(advertising.Current);
            Assert.True(session.IsDiscovering, "Advertising and discovery are independent.");
        }

        [Fact]
        public async Task DiscoverFaultAfterSuccessfulStart_PublishesFalse_AndLeavesAdvertisingRunning()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            await using var discovery = session.DiscoveryChanges.GetAsyncEnumerator(TestContext.Current.CancellationToken);

            // Act
            connections.FaultDiscoverStream(new NearbyDiscoveryException("radio dropped"));

            // Assert
            Assert.True(await discovery.MoveNextAsync());
            Assert.False(discovery.Current);
            Assert.True(session.IsAdvertising, "Advertising and discovery are independent.");
        }

        [Fact]
        public async Task DiscoveryRefresh_PublishesNothing_AndIsDiscoveringStaysTrue()
        {
            // A refresh restarts the underlying scan. Discovery never logically stopped, so the
            // flag holds and the stream stays silent — otherwise a bound indicator blinks every
            // refresh interval.

            // Arrange
            var time = new FakeTimeProvider();
            var interval = TimeSpan.FromSeconds(30);
            var connections = new FakeNearby();
            var session = Create.Session(connections, new NearbyOptions { DiscoveryRefreshInterval = interval }, time);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            await using var changes = session.DiscoveryChanges.GetAsyncEnumerator(TestContext.Current.CancellationToken);

            // Act
            time.Advance(interval);
            await Wait.UntilAsync(() => connections.DiscoverCallCount >= 2);

            // Assert
            Assert.True(session.IsDiscovering, "Discovery does not stop across a refresh.");

            await session.StopDiscoveryAsync(TestContext.Current.CancellationToken);
            Assert.True(await changes.MoveNextAsync());
            Assert.False(changes.Current, "The refresh must not have published a false/true blink before the real stop.");
        }
    }

    public sealed class Hazards : NearbyTests
    {
        [Fact]
        public async Task EnumeratingDevices_WhileCollectionMutates_DoesNotThrow()
        {
            // Ports the *hazard* behind ConnectionLifecycleAdversarialTests rather than the test:
            // the specific bug died with ConnectionLifecycle, but handing consumers a live
            // collection makes "collection was modified during enumeration" newly reachable.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            for (var i = 0; i < 50; i++)
            {
                await connections.EmitDeviceFoundAsync(new NearbyDevice($"peer-{i}", $"Device {i}"));
            }

            var mutating = Task.Run(async () =>
            {
                for (var i = 50; i < 150; i++)
                {
                    await connections.EmitDeviceFoundAsync(new NearbyDevice($"peer-{i}", $"Device {i}"));
                }
            }, TestContext.Current.CancellationToken);

            // Act — a consumer snapshotting the collection must not observe a torn enumeration.
            for (var pass = 0; pass < 50; pass++)
            {
                _ = session.Devices.ToArray().Length;
            }

            await mutating;

            // Assert
            Assert.True(session.Devices.Count >= 150);
        }

        [Fact]
        public async Task AbandonedWatcher_DoesNotBreakTheSession()
        {
            // The structural win over events: a consumer cannot run code on the callback path, so a
            // broken consumer cannot take the session down with it. Here the watcher simply stops
            // reading — with an unbounded per-watcher channel that must not block the publisher.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            using var abandonedToken = new CancellationTokenSource();
            var abandoned = session.Devices.Changes.GetAsyncEnumerator(abandonedToken.Token);

            // Start the enumerator so it subscribes, but never await this: the point is a watcher
            // that has a live channel and is not draining it. Awaiting here would block forever —
            // nothing has been published yet — which is a property of the stream, not a defect.
            var neverAwaited = abandoned.MoveNextAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            // Act
            var connection = await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await abandonedToken.CancelAsync();

            // Expected: cancelling is how an abandoned watcher is torn down.
            try
            {
                await neverAwaited;
            }
            catch (OperationCanceledException)
            {
            }

            // Assert
            Assert.NotNull(connection);
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
            await abandoned.DisposeAsync();
        }

        [Fact]
        public async Task WatchCycles_LeaveTheSurvivingWatcherReceivingExactlyOnce()
        {
            // R-6, the leak class that motivated the whole restructure. Five enter/leave page
            // visits: with events, a subscription without a matching `-=` fired five times per
            // event. Ending the enumeration is now the only cleanup, and it cannot be forgotten —
            // `await using` does it, and so does breaking out of an `await foreach`.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            for (var visit = 0; visit < 5; visit++)
            {
                await using var transient = new ChangeRecorder(session);
            }

            // Sixth visit: still watching when the change happens.
            await using var recorder = new ChangeRecorder(session);

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await recorder.WaitForAsync("peer-1", 1);

            // Assert
            // Each change must reach a live watcher once, not once per past page visit.
            Assert.Single(recorder.StatusesFor("peer-1"), st => st is NearbyDeviceStatus.Connected);
        }

        [Fact]
        public async Task StopAsync_RejectsOutstandingRequests()
        {
            // Otherwise the remote device waits on a request nobody will ever answer.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            var rejected = false;
            await connections.EmitRequestAsync(
                device,
                () => Create.Connection(device),
                onReject: () => rejected = true);

            // Act
            await session.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(rejected);
            Assert.Empty(session.Devices);
        }
    }

    // -------------------------------------------------------------------------
    // Backgrounding teardown (see docs/ARCHITECTURE.md section 5 — the migration map holds the
    // product-scope record).
    //
    // On iOS, AppLifecycleObserver calls StopAsync when the app enters the
    // background, because MultipeerConnectivity does not survive suspension and
    // the plugin would otherwise report a session iOS has already killed.
    //
    // The observer itself is iOS-only and needs UIKit, so it cannot be
    // instantiated on net10.0. What these pin instead is the contract it depends
    // on: that StopAsync alone produces every consumer-visible transition
    // backgrounding requires. If one of these regresses, the iOS fix silently
    // stops delivering the state the consumer needs, with no compile error.
    // -------------------------------------------------------------------------

    public sealed class BackgroundTeardown : NearbyTests
    {
        [Fact]
        public async Task StopAsync_RaisesConnectionDropped_ForEveryLiveConnection()
        {
            // The zombie-Connected bug: without this, a consumer backgrounded mid-conversation
            // is never told the connection ended, because iOS tears MPC down silently and with
            // no NSError. The change stream is the only signal it will ever get.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            Assert.Equal(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));

            await using var recorder = new ChangeRecorder(session);

            await session.StopAsync(TestContext.Current.CancellationToken);

            // Two independent async paths settle here: the registry publishes Removed, and the
            // platform's connection table clears once Disconnected completes (the release path).
            // Waiting on the change alone races the second, so wait for both.
            await Wait.UntilAsync(() => recorder.For("peer-1")
                .Any(c => c.Action is NearbyDeviceChangeAction.Removed)
                && !session.TryGetConnection("peer-1", out _));

            // Removal is how a stopped session reports the device is gone; the connection going
            // away is what the backgrounded consumer must be able to observe.

            // Act
            var removals = recorder.For("peer-1")
                .Where(c => c.Action is NearbyDeviceChangeAction.Removed)
                .ToArray();

            // Assert
            Assert.Single(removals);
            Assert.False(session.TryGetConnection("peer-1", out _));
        }

        [Fact]
        public async Task StopAsync_ClearsConnectedState_SoNoDeviceIsLeftReportingConnected()
        {
            // Devices is the state consumers bind to. A row still reading Connected after the OS
            // ended the session is precisely the state this fix exists to eliminate.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            await session.StopAsync(TestContext.Current.CancellationToken);

            Assert.Empty(session.Devices);

            // Awaited, not asserted outright: the per-device clear happens in WatchDisconnectAsync,
            // which observes the connection's Disconnected task and therefore lands after StopAsync
            // returns. See StopAsync_ClearingDeviceState_IsNotSynchronous for why that ordering is
            // load-bearing on iOS rather than an incidental detail.

            // Act
            await Wait.UntilAsync(() =>
                device.Status is NearbyDeviceStatus.Visible && !session.TryGetConnection(device.Id, out _));

            // Assert
            Assert.NotEqual(NearbyDeviceStatus.Connected, device.Status);
            Assert.False(
                session.TryGetConnection(device.Id, out _),
                "A cleared device must not still resolve to a dead connection.");
        }

        [Fact]
        public async Task StopAsync_ClearingDeviceState_IsNotSynchronous()
        {
            // Documents a real hazard rather than asserting desired behaviour. StopAsync clears the
            // Devices collection synchronously, but per-device Status and Role, and the connection
            // lookup entry, are cleared by WatchDisconnectAsync, which runs as a continuation on the
            // connection's Disconnected task. So immediately after StopAsync returns, a device the
            // caller still holds a reference to can briefly report Connected with a resolvable
            // connection.
            //
            // Consumers binding to Devices never see this — the row is already gone. It matters on
            // iOS: AppLifecycleObserver cannot await teardown (UIKit gives seconds before
            // suspension), so the process may suspend before this continuation runs. That is
            // acceptable because iOS has already destroyed the transport and the state is rebuilt
            // from scratch on foreground — but if this ever needs to be synchronous, this test is
            // the one that will fail and say why.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            await session.StopAsync(TestContext.Current.CancellationToken);

            // The collection consumers bind to is cleared synchronously.
            Assert.Empty(session.Devices);
            // Wait on every condition asserted below, not just the first. Status and the connection
            // lookup are cleared on the same continuation but not atomically, so polling one and
            // asserting the other can observe the gap between them.

            // Act
            await Wait.UntilAsync(() =>
                device.Status is NearbyDeviceStatus.Visible && !session.TryGetConnection(device.Id, out _));

            // Assert
            // Per-device state is cleared, just asynchronously.
            Assert.Equal(NearbyDeviceStatus.Visible, device.Status);
            Assert.False(session.TryGetConnection(device.Id, out _), "The connection lookup is cleared on the same continuation.");
        }

        [Fact]
        public async Task StopAsync_ClearsBothToggles_SoNeitherReportsScanningWhileSuspended()
        {
            // The second zombie state. While suspended nothing is advertising or scanning, so
            // leaving these true would misreport the radio just as Connected misreported the session.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Act
            await session.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(session.IsAdvertising);
            Assert.False(session.IsDiscovering);
        }

        [Fact]
        public async Task StopAsync_LeavesSessionReusable_SoTheAppCanStartAgainOnForeground()
        {
            // Nothing restarts automatically: the app calls Start* again on foreground. That is
            // only viable if StopAsync leaves the session usable rather than terminally torn down.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            await session.StopAsync(TestContext.Current.CancellationToken);
            Assert.False(session.IsDiscovering);

            // Act
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(session.IsDiscovering);
            // Restart must reach the platform, not be swallowed as a no-op.
            Assert.Equal(2, connections.DiscoverCallCount);
        }

        [Fact]
        public async Task StopAsync_IsIdempotent_SoARepeatedBackgroundNotificationIsHarmless()
        {
            // DidEnterBackground can arrive more than once across a suspend/resume cycle, and the
            // observer does not deduplicate — it relies on StopAsync being safe to call again.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync(TestContext.Current.CancellationToken);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);

            await using var recorder = new ChangeRecorder(session);

            await session.StopAsync(TestContext.Current.CancellationToken);
            await session.StopAsync(TestContext.Current.CancellationToken);
            await session.StopAsync(TestContext.Current.CancellationToken);

            // Act
            await Wait.UntilAsync(() => recorder.For("peer-1")
                .Any(c => c.Action is NearbyDeviceChangeAction.Removed));

            // Assert
            // A device must be reported removed once per connection, not once per StopAsync call.
            Assert.Single(recorder.For("peer-1"), c => c.Action is NearbyDeviceChangeAction.Removed);
            Assert.False(session.IsAdvertising);
            Assert.False(session.IsDiscovering);
        }
    }

}
