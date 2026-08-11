using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Behavioural tests for <see cref="NearbyImplementation"/>.
/// </summary>
/// <remarks>
/// The session takes <see cref="IPlatformNearby"/> rather than the concrete implementation
/// precisely so these can run on <c>net10.0</c>, where every <c>Platform*</c> start throws.
/// </remarks>
[TestCategory("Session")]
public class NearbyImplementationTests
{
    // -------------------------------------------------------------------------
    // Preflight availability
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class CheckAvailability : NearbyImplementationTests
    {
        [TestMethod]
        public async Task Always_DelegatesToThePlatform()
        {
            // Arrange
            var connections = new FakeNearby { Availability = NearbyAvailability.Ready };
            var session = Create.Session(connections);

            // Act
            var result = await session.CheckAvailabilityAsync();

            // Assert
            Assert.AreEqual(NearbyAvailability.Ready, result);
            Assert.AreEqual(1, connections.CheckAvailabilityCallCount);
        }

        [TestMethod]
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
            var result = await session.CheckAvailabilityAsync();

            // Assert
            Assert.IsTrue(result.HasFlag(NearbyAvailability.BluetoothDisabled));
            Assert.IsTrue(result.HasFlag(NearbyAvailability.MissingPermissions));
            Assert.IsFalse(result.HasFlag(NearbyAvailability.PlayServicesUnavailable));
        }

        [TestMethod]
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
            var result = await session.CheckAvailabilityAsync();

            // Assert
            Assert.AreNotEqual(NearbyAvailability.Ready, result);
        }

        [TestMethod]
        public async Task DoesNotStartAdvertisingOrDiscovery()
        {
            // A preflight check must not have side effects: it reports state, it does not mutate it.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.CheckAvailabilityAsync();

            // Assert
            Assert.IsFalse(session.IsAdvertising);
            Assert.IsFalse(session.IsDiscovering);
            Assert.AreEqual(0, connections.AdvertiseCallCount);
            Assert.AreEqual(0, connections.DiscoverCallCount);
        }

        [TestMethod]
        public async Task CanceledToken_Throws()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Assert
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => session.CheckAvailabilityAsync(cts.Token));
        }
    }

    // -------------------------------------------------------------------------
    // Advertising / discovery toggles — decision 8: they are independent.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Toggles : NearbyImplementationTests
    {
        [TestMethod]
        public async Task StartAdvertisingAsync_SetsIsAdvertising_WithoutSettingIsDiscovering()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.StartAdvertisingAsync();

            // Assert
            Assert.IsTrue(session.IsAdvertising);
            Assert.IsFalse(session.IsDiscovering, "Advertising must not imply discovering — both sample pages toggle them separately.");
        }

        [TestMethod]
        public async Task StartDiscoveryAsync_SetsIsDiscovering_WithoutSettingIsAdvertising()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.StartDiscoveryAsync();

            // Assert
            Assert.IsTrue(session.IsDiscovering);
            Assert.IsFalse(session.IsAdvertising);
        }

        [TestMethod]
        public async Task StopAdvertisingAsync_ClearsIsAdvertising_AndLeavesDiscoveryRunning()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();
            await session.StartDiscoveryAsync();

            // Act
            await session.StopAdvertisingAsync();

            // Assert
            Assert.IsFalse(session.IsAdvertising);
            Assert.IsTrue(session.IsDiscovering, "Stopping one must not stop the other.");
        }

        [TestMethod]
        public async Task StopAsync_ClearsBothToggles()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();
            await session.StartDiscoveryAsync();

            // Act
            await session.StopAsync();

            // Assert
            Assert.IsFalse(session.IsAdvertising);
            Assert.IsFalse(session.IsDiscovering);
        }

        [TestMethod]
        public async Task StartAdvertisingAsync_CalledTwice_IsNoOp()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Act
            await session.StartAdvertisingAsync();
            await session.StartAdvertisingAsync();

            // Assert
            Assert.IsTrue(session.IsAdvertising);
            Assert.AreEqual(1, connections.AdvertiseCallCount, "A second start must not reach the platform again.");
        }
    }

    // -------------------------------------------------------------------------
    // Start failures. Previously delivered as a faulted stream observed at first
    // enumeration; the pump now reports them and clears the toggle.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class StartFailures : NearbyImplementationTests
    {
        [TestMethod]
        public async Task AdvertisePumpFailure_ClearsIsAdvertising()
        {
            // Arrange
            var connections = new FakeNearby
            {
                AdvertiseFault = new NearbyAdvertisingException("radio off"),
            };
            var session = Create.Session(connections);

            // Act
            await session.StartAdvertisingAsync();
            await connections.WaitForAdvertisePumpAsync();

            // Assert
            Assert.IsFalse(session.IsAdvertising, "A failed start must not leave the session claiming to advertise.");
        }

        [TestMethod]
        public async Task DiscoverPumpFailure_ClearsIsDiscovering()
        {
            // Arrange
            var connections = new FakeNearby
            {
                DiscoverFault = new NearbyDiscoveryException("permission denied"),
            };
            var session = Create.Session(connections);

            // Act
            await session.StartDiscoveryAsync();
            await connections.WaitForDiscoverPumpAsync();

            // Assert
            Assert.IsFalse(session.IsDiscovering);
        }
    }

    // -------------------------------------------------------------------------
    // Discovery projected onto the Devices collection.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Discovery : NearbyImplementationTests
    {
        [TestMethod]
        public async Task DeviceFound_AddsToDevices()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            // Assert
            Assert.HasCount(1, session.Devices);
            Assert.AreSame(device, session.Devices[0]);
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        [TestMethod]
        public async Task DeviceFound_Twice_DoesNotDuplicate()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            await connections.EmitDeviceFoundAsync(device);

            // Assert
            Assert.HasCount(1, session.Devices);
        }

        [TestMethod]
        public async Task DeviceLost_RemovesVisibleDevice()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            await connections.EmitDeviceLostAsync(device);

            // Assert
            Assert.IsEmpty(session.Devices);
        }

        [TestMethod]
        public async Task DeviceLost_DoesNotRemoveConnectedDevice()
        {
            // Going out of discovery range is not the same as disconnecting. Removing a connected
            // device here would delete a live conversation from the UI.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device);

            // Act
            await connections.EmitDeviceLostAsync(device);

            // Assert
            Assert.HasCount(1, session.Devices);
            Assert.AreEqual(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
        }

        [TestMethod]
        public async Task StopDiscoveryAsync_DrainsVisibleDevices()
        {
            // Otherwise the UI shows devices that are no longer being looked for, forever.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-1", "Alice"));
            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-2", "Bob"));

            // Act
            await session.StopDiscoveryAsync();

            // Assert
            Assert.IsEmpty(session.Devices);
        }

        [TestMethod]
        public async Task StopDiscoveryAsync_KeepsConnectedDevices()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device);

            // Act
            await session.StopDiscoveryAsync();

            // Assert
            Assert.HasCount(1, session.Devices);
        }
    }

    // -------------------------------------------------------------------------
    // Inbound requests: accept / reject.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class InboundRequests : NearbyImplementationTests
    {
        [TestMethod]
        public async Task RequestArriving_ReportsRequestReceived_AndSurfacesDevice()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();

            await using var recorder = new ChangeRecorder(session);

            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await recorder.WaitForAsync("peer-1", 2);

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.RequestReceived, session.StatusOf("peer-1"));
            Assert.Contains(device, session.Devices);

            // Added before Updated: a consumer must never see a status change for a device it has
            // not been told about.
            Assert.AreSequenceEqual(
                new[] { NearbyDeviceChangeAction.Added, NearbyDeviceChangeAction.Updated },
                recorder.For("peer-1").Select(c => c.Action).ToArray());
        }

        [TestMethod]
        public async Task AutoAccept_ConnectsWithoutEverReportingRequestReceived()
        {
            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync();

            await using var recorder = new ChangeRecorder(session);
            var device = new NearbyDevice("peer-1", "Alice");

            await connections.EmitRequestAsync(device, () => Create.Connection(device));

            await recorder.WaitForAsync("peer-1", 2);

            // Act
            Assert.AreEqual(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));

            // The documented contract of auto-accept: the state is skipped, not merely unreported.

            // Assert
            Assert.DoesNotContain(
                NearbyDeviceStatus.RequestReceived,
                recorder.StatusesFor("peer-1"),
                "RequestReceived must not be observable when requests are auto-accepted.");
        }

        [TestMethod]
        public async Task AutoAccept_LeavesNoPendingRequestToAnswer()
        {
            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync();
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await connections.EmitRequestAsync(device, () => Create.Connection(device));

            // The session already answered, so there is nothing left for the application to accept.

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.AcceptAsync(device));
        }

        [TestMethod]
        public async Task AutoAccept_WhenAcceptFails_ResetsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var session = Create.Session(connections, options);
            await session.StartAdvertisingAsync();
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await connections.EmitRequestAsync(
                device,
                () => throw new NearbyException("Handshake failed."));

            // A failed auto-accept must not strand the row on Connecting, and must not escape into
            // the advertise pump and stop advertising.

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
            Assert.IsTrue(session.IsAdvertising);
        }

        [TestMethod]
        public async Task AcceptAsync_ConnectsAndReportsConnected()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            var connection = Create.Connection(device);
            await connections.EmitRequestAsync(device, () => connection);

            await using var recorder = new ChangeRecorder(session);

            // Act
            var result = await session.AcceptAsync(device);

            // Assert
            Assert.AreSame(connection, result);
            Assert.AreEqual(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
            Assert.AreEqual(ConnectionRole.Acceptor, session.Current("peer-1")!.Role);
            Assert.IsTrue(session.TryGetConnection(device.Id, out var lookedUp));
            Assert.AreSame(connection, lookedUp);

            // Wait for the Connected change specifically. A plain count reaches 1 as soon as any
            // change lands, which may be an earlier transition in the handshake.
            await Wait.UntilAsync(
                () => recorder.StatusesFor("peer-1").Contains(NearbyDeviceStatus.Connected));

            Assert.Contains(NearbyDeviceStatus.Connected, recorder.StatusesFor("peer-1"));
        }

        [TestMethod]
        public async Task RejectAsync_DoesNotConnect()
        {
            // Security-relevant: rejecting must never produce a connection.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => Create.Connection(device));

            await using var recorder = new ChangeRecorder(session);

            // Act
            await session.RejectAsync(device);

            // Assert
            Assert.DoesNotContain(NearbyDeviceStatus.Connected, recorder.StatusesFor("peer-1"));
            Assert.IsFalse(session.TryGetConnection(device.Id, out _));
            Assert.AreEqual(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
        }

        [TestMethod]
        public async Task AcceptAsync_AfterReject_Throws()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => Create.Connection(device));
            await session.RejectAsync(device);

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.AcceptAsync(device));
        }

        [TestMethod]
        public async Task AcceptAsync_WithNoOutstandingRequest_Throws()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => session.AcceptAsync(new NearbyDevice("peer-1", "Alice")));
        }

        [TestMethod]
        public async Task AcceptAsync_WhenPlatformFails_ResetsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(
                device,
                () => throw new InvalidOperationException("handshake failed"));

            // Act
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.AcceptAsync(device));

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"), "A failed handshake must not strand the row on Connecting.");
        }
    }

    // -------------------------------------------------------------------------
    // Outbound connect.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Connect : NearbyImplementationTests
    {
        [TestMethod]
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
            var result = await session.ConnectAsync(device);

            // Assert
            Assert.AreSame(connection, result);
            Assert.AreEqual(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));
            Assert.AreEqual(ConnectionRole.Initiator, session.Current("peer-1")!.Role);
            Assert.IsTrue(session.TryGetConnection(device.Id, out _));

            await Wait.UntilAsync(
                () => recorder.StatusesFor("peer-1").Contains(NearbyDeviceStatus.Connected));

            Assert.Contains(
                NearbyDeviceStatus.Connected,
                recorder.StatusesFor("peer-1"),
                "Reaching Connected must be reported as a change, not only reflected in Devices.");
        }

        [TestMethod]
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
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.ConnectAsync(device));

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        [TestMethod]
        public async Task ConnectAsync_NullDevice_Throws()
        {
            // Arrange
            var session = Create.Session(new FakeNearby());

            // Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => session.ConnectAsync(null!));
        }

    }

    // -------------------------------------------------------------------------
    // Disconnect. Guards P2-3: the drop must be reported exactly once, whichever
    // side ended it.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Disconnect : NearbyImplementationTests
    {
        [TestMethod]
        public async Task RemoteDisconnect_RaisesConnectionDroppedExactlyOnce()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = Create.Connection(device);
            connections.ConnectResult = connection;

            await session.ConnectAsync(device);

            await using var recorder = new ChangeRecorder(session);

            // Act
            await connection.DisposeAsync();
            await Wait.UntilAsync(() => session.StatusOf("peer-1") is NearbyDeviceStatus.Visible);

            // Assert
            Assert.HasCount(
                1,
                recorder.StatusesFor("peer-1"),
                "A duplicate drop was a previously fixed bug (P2-3).");
            Assert.AreEqual(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
        }

        [TestMethod]
        public async Task DisconnectAsync_ReportsTheDropExactlyOnce()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            await session.ConnectAsync(device);

            await using var recorder = new ChangeRecorder(session);

            // Act
            await session.DisconnectAsync(device);
            await Wait.UntilAsync(() => session.StatusOf("peer-1") is NearbyDeviceStatus.Visible);

            // Assert
            Assert.HasCount(
                1,
                recorder.StatusesFor("peer-1"),
                "A local disconnect must be reported exactly like a remote one.");
        }

        [TestMethod]
        public async Task DisconnectAsync_WhenNotConnected_IsNoOp()
        {
            // Arrange
            var session = Create.Session(new FakeNearby());
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await session.DisconnectAsync(device);

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        // GAP: the reason a connection ended is no longer observable by consumers. It travelled on
        // NearbyConnectionChangedEventArgs.Reason, and a device back in Visible carries no reason.
        // EndReason now reaches logs only. This test asserts what a consumer CAN still see, and
        // exists to be rewritten when a reason is reattached to the transition.
        [TestMethod]
        public async Task ReturnsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            // Act
            await session.ConnectAsync(device);
            await session.DisconnectAsync(device);
            await Wait.UntilAsync(() => session.StatusOf("peer-1") is NearbyDeviceStatus.Visible);

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, session.StatusOf("peer-1"));
            Assert.IsNull(session.Current("peer-1")!.Role, "A disconnected device plays no role.");
        }

        // DiscoveryPageViewModel filters on "Visible or Connecting". A device left in any other
        // state after a drop silently vanishes from the discovery list — a capability regression
        // that no other test in this suite would catch.
        [TestMethod]
        public async Task DroppedDevice_RejoinsTheDiscoveryFilter()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            // Act
            await session.ConnectAsync(device);
            await session.DisconnectAsync(device);
            await Wait.UntilAsync(() => session.StatusOf("peer-1") is not NearbyDeviceStatus.Connected);

            // Assert
            Assert.IsTrue(
                session.StatusOf("peer-1") is NearbyDeviceStatus.Visible or NearbyDeviceStatus.Connecting,
                $"A dropped device must rejoin the discovery filter, but was {session.StatusOf("peer-1")}.");
        }

        // Cancelled, TimedOut and Failed are branched per exception type rather than defaulted, so
        // a caller that withdraws is not reported as a failure.
        [TestMethod]
        public async Task ConnectAsync_Cancelled_LeavesDeviceVisible()
        {
            // Arrange
            var connections = new FakeNearby { ConnectFault = new OperationCanceledException() };
            var session = Create.Session(connections);
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => session.ConnectAsync(device));

            // Assert
            Assert.AreEqual(
                NearbyDeviceStatus.Visible,
                session.StatusOf("peer-1"),
                "A cancelled handshake must not strand the row on Connecting.");
            Assert.IsNull(session.Current("peer-1")!.Role, "A device that is not connecting carries no role.");
        }

        [TestMethod]
        public async Task DisconnectAsync_LeavesOtherConnectionsIntact()
        {
            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);

            var alice = new NearbyDevice("peer-1", "Alice");
            var bob = new NearbyDevice("peer-2", "Bob");

            connections.ConnectResult = Create.Connection(alice);
            await session.ConnectAsync(alice);
            connections.ConnectResult = Create.Connection(bob);
            await session.ConnectAsync(bob);

            await session.DisconnectAsync(alice);

            // Alice's entry is removed on the Disconnected continuation, not inside DisconnectAsync
            // — the same asynchrony documented by StopAsync_ClearingDeviceState_IsNotSynchronous.

            // Act
            await Wait.UntilAsync(() => !session.TryGetConnection(alice.Id, out _));

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Connected, session.StatusOf("peer-2"));
            Assert.IsTrue(session.TryGetConnection(bob.Id, out _), "Disconnecting one device must not tear down the others.");
        }
    }

    // -------------------------------------------------------------------------
    // Payload delivery — the stream survives untouched by this restructure.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Payloads : NearbyImplementationTests
    {
        [TestMethod]
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
            var aliceConnection = await session.ConnectAsync(alice);
            connections.ConnectResult = Create.Connection(bob, bobChannel);
            var bobConnection = await session.ConnectAsync(bob);

            aliceChannel.Writer.TryWrite(new NearbyBytesPayload([1]));
            bobChannel.Writer.TryWrite(new NearbyBytesPayload([2]));
            aliceChannel.Writer.TryComplete();
            bobChannel.Writer.TryComplete();

            var aliceCount = 0;
            var bobCount = 0;

            await foreach (var _ in aliceConnection.ReceiveAsync())
            {
                aliceCount++;
        }

            // Act
            await foreach (var _ in bobConnection.ReceiveAsync())
            {
                bobCount++;
        }

            // Assert
            Assert.AreEqual(1, aliceCount);
            Assert.AreEqual(1, bobCount);
        }
    }

    // -------------------------------------------------------------------------
    // Hazards flagged by the test-mining pass.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Hazards : NearbyImplementationTests
    {
        [TestMethod]
        public async Task EnumeratingDevices_WhileCollectionMutates_DoesNotThrow()
        {
            // Ports the *hazard* behind ConnectionLifecycleAdversarialTests rather than the test:
            // the specific bug died with ConnectionLifecycle, but handing consumers a live
            // collection makes "collection was modified during enumeration" newly reachable.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

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
        });

            // A consumer snapshotting the collection must not observe a torn enumeration.
            for (var pass = 0; pass < 50; pass++)
            {
                _ = session.Devices.ToArray().Length;
        }

            // Act
            await mutating;

            // Assert
            Assert.IsGreaterThanOrEqualTo(150, session.Devices.Count);
        }

        [TestMethod]
        public async Task AbandonedWatcher_DoesNotBreakTheSession()
        {
            // The structural win over events: a consumer cannot run code on the callback path, so a
            // broken consumer cannot take the session down with it. Here the watcher simply stops
            // reading — with an unbounded per-watcher channel that must not block the publisher.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            using var abandonedToken = new CancellationTokenSource();
            var abandoned = session.Devices.Changes.GetAsyncEnumerator(abandonedToken.Token);

            // Start the enumerator so it subscribes, but never await this: the point is a watcher
            // that has a live channel and is not draining it. Awaiting here would block forever —
            // nothing has been published yet — which is a property of the stream, not a defect.
            var neverAwaited = abandoned.MoveNextAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            var connection = await session.ConnectAsync(device);

            Assert.IsNotNull(connection);
            Assert.AreEqual(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));

            await abandonedToken.CancelAsync();

                // Expected: cancelling is how an abandoned watcher is torn down.

            // Act
            try
            {
                await neverAwaited;
            }
            catch (OperationCanceledException)
            {
            }

            // Assert
            await abandoned.DisposeAsync();
        }

        [TestMethod]
        public async Task WatchCycles_LeaveTheSurvivingWatcherReceivingExactlyOnce()
        {
            // R-6, the leak class that motivated the whole restructure. Five enter/leave page
            // visits: with events, a subscription without a matching `-=` fired five times per
            // event. Ending the enumeration is now the only cleanup, and it cannot be forgotten —
            // `await using` does it, and so does breaking out of an `await foreach`.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            for (var visit = 0; visit < 5; visit++)
            {
                await using var transient = new ChangeRecorder(session);
            }

            // Sixth visit: still watching when the change happens.
            await using var recorder = new ChangeRecorder(session);

            // Act
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device);
            await recorder.WaitForAsync("peer-1", 1);

            // Assert
            Assert.HasCount(
                1,
                recorder.StatusesFor("peer-1").Where(st => st is NearbyDeviceStatus.Connected),
                "Each change must reach a live watcher once, not once per past page visit.");
        }

        [TestMethod]
        public async Task StopAsync_RejectsOutstandingRequests()
        {
            // Otherwise the remote device waits on a request nobody will ever answer.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            var rejected = false;
            await connections.EmitRequestAsync(
                device,
                () => Create.Connection(device),
                onReject: () => rejected = true);

            // Act
            await session.StopAsync();

            // Assert
            Assert.IsTrue(rejected);
            Assert.IsEmpty(session.Devices);
        }
    }

    // -------------------------------------------------------------------------
    // Backgrounding teardown (see docs/DECISIONS.md — "Product scope").
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

    [TestClass]
    public sealed class BackgroundTeardown : NearbyImplementationTests
    {
        [TestMethod]
        public async Task StopAsync_RaisesConnectionDropped_ForEveryLiveConnection()
        {
            // The zombie-Connected bug: without this, a consumer backgrounded mid-conversation
            // is never told the connection ended, because iOS tears MPC down silently and with
            // no NSError. The change stream is the only signal it will ever get.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);

            await session.ConnectAsync(device);
            Assert.AreEqual(NearbyDeviceStatus.Connected, session.StatusOf("peer-1"));

            await using var recorder = new ChangeRecorder(session);

            await session.StopAsync();
            await Wait.UntilAsync(() => recorder.For("peer-1").Count > 0);

            // Removal is how a stopped session reports the device is gone; the connection going
            // away is what the backgrounded consumer must be able to observe.

            // Act
            var removals = recorder.For("peer-1")
                .Where(c => c.Action is NearbyDeviceChangeAction.Removed)
                .ToArray();

            // Assert
            Assert.HasCount(1, removals);
            Assert.IsFalse(session.TryGetConnection("peer-1", out _));
        }

        [TestMethod]
        public async Task StopAsync_ClearsConnectedState_SoNoDeviceIsLeftReportingConnected()
        {
            // Devices is the state consumers bind to. A row still reading Connected after the OS
            // ended the session is precisely the state this fix exists to eliminate.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device);

            await session.StopAsync();

            Assert.IsEmpty(session.Devices);

            // Awaited, not asserted outright: the per-device clear happens in WatchDisconnectAsync,
            // which observes the connection's Disconnected task and therefore lands after StopAsync
            // returns. See StopAsync_ClearingDeviceState_IsNotSynchronous for why that ordering is
            // load-bearing on iOS rather than an incidental detail.

            // Act
            await Wait.UntilAsync(() =>
                device.Status is NearbyDeviceStatus.Visible && !session.TryGetConnection(device.Id, out _));

            // Assert
            Assert.AreNotEqual(NearbyDeviceStatus.Connected, device.Status);
            Assert.IsFalse(
                session.TryGetConnection(device.Id, out _),
                "A cleared device must not still resolve to a dead connection.");
        }

        [TestMethod]
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
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device);

            await session.StopAsync();

            Assert.IsEmpty(session.Devices, "The collection consumers bind to is cleared synchronously.");

            // Wait on every condition asserted below, not just the first. Status and the connection
            // lookup are cleared on the same continuation but not atomically, so polling one and
            // asserting the other can observe the gap between them.

            // Act
            await Wait.UntilAsync(() =>
                device.Status is NearbyDeviceStatus.Visible && !session.TryGetConnection(device.Id, out _));

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status, "Per-device state is cleared, just asynchronously.");
            Assert.IsFalse(session.TryGetConnection(device.Id, out _), "The connection lookup is cleared on the same continuation.");
        }

        [TestMethod]
        public async Task StopAsync_ClearsBothToggles_SoNeitherReportsScanningWhileSuspended()
        {
            // The second zombie state. While suspended nothing is advertising or scanning, so
            // leaving these true would misreport the radio just as Connected misreported the session.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();
            await session.StartDiscoveryAsync();

            // Act
            await session.StopAsync();

            // Assert
            Assert.IsFalse(session.IsAdvertising);
            Assert.IsFalse(session.IsDiscovering);
        }

        [TestMethod]
        public async Task StopAsync_LeavesSessionReusable_SoTheAppCanStartAgainOnForeground()
        {
            // Nothing restarts automatically: the app calls Start* again on foreground. That is
            // only viable if StopAsync leaves the session usable rather than terminally torn down.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartDiscoveryAsync();

            await session.StopAsync();
            Assert.IsFalse(session.IsDiscovering);

            // Act
            await session.StartDiscoveryAsync();

            // Assert
            Assert.IsTrue(session.IsDiscovering);
            Assert.AreEqual(2, connections.DiscoverCallCount, "Restart must reach the platform, not be swallowed as a no-op.");
        }

        [TestMethod]
        public async Task StopAsync_IsIdempotent_SoARepeatedBackgroundNotificationIsHarmless()
        {
            // DidEnterBackground can arrive more than once across a suspend/resume cycle, and the
            // observer does not deduplicate — it relies on StopAsync being safe to call again.

            // Arrange
            var connections = new FakeNearby();
            var session = Create.Session(connections);
            await session.StartAdvertisingAsync();
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = Create.Connection(device);
            await session.ConnectAsync(device);

            await using var recorder = new ChangeRecorder(session);

            await session.StopAsync();
            await session.StopAsync();
            await session.StopAsync();

            // Act
            await Wait.UntilAsync(() => recorder.For("peer-1").Count > 0);

            // Assert
            Assert.HasCount(
                1,
                recorder.For("peer-1").Where(c => c.Action is NearbyDeviceChangeAction.Removed),
                "A device must be reported removed once per connection, not once per StopAsync call.");
            Assert.IsFalse(session.IsAdvertising);
            Assert.IsFalse(session.IsDiscovering);
        }
    }

}
