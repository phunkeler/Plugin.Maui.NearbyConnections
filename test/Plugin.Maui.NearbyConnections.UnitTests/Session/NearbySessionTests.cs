using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Behavioural tests for <see cref="NearbySession"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces <c>NearbyAdvertiserTests</c> + <c>NearbyDiscovererTests</c> (2,435 lines of two
/// near-identical mirrors). Those files asserted the mechanics of the event-union/broadcaster design
/// that no longer exists; what survives here is the consumer-visible behaviour they guarded, stated
/// once. See <c>.building/notes/TEST-MINING.md</c> for the per-test classification.
/// </para>
/// <para>
/// The session takes <see cref="INearbyConnections"/> rather than the concrete implementation
/// precisely so these can run on <c>net10.0</c>, where every <c>Platform*</c> start throws.
/// </para>
/// </remarks>
[TestCategory("Session")]
public class NearbySessionTests
{
    static NearbySession CreateSut(FakeNearbyConnections connections)
        => new(connections, dispatcher: null, NullLogger.Instance);

    static NearbySession CreateSut(FakeNearbyConnections connections, ILogger logger)
        => new(connections, dispatcher: null, logger);

    /// <summary>
    /// Captures log records so tests can assert on diagnostics that are the only observable
    /// evidence of a misuse — there is no state change or exception to assert against.
    /// </summary>
    sealed class CapturingLogger : ILogger
    {
        readonly List<(LogLevel Level, string Message)> _records = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Records
        {
            get { lock (_records) { return [.. _records]; } }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_records)
            {
                _records.Add((logLevel, formatter(state, exception)));
            }
        }
    }

    static NearbyConnection CreateConnection(NearbyDevice device, Channel<NearbyPayload>? channel = null)
        => new(
            device,
            channel ?? Channel.CreateUnbounded<NearbyPayload>(),
            sendBytesFactory: (_, _) => ValueTask.CompletedTask,
            sendFileFactory: (_, _, _) => Task.CompletedTask,
            disposeFactory: () => ValueTask.CompletedTask);

    // -------------------------------------------------------------------------
    // Advertising / discovery toggles — decision 8: they are independent.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Toggles : NearbySessionTests
    {
        [TestMethod]
        public async Task StartAdvertisingAsync_SetsIsAdvertising_WithoutSettingIsDiscovering()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);

            await sut.StartAdvertisingAsync();

            Assert.IsTrue(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering, "Advertising must not imply discovering — both sample pages toggle them separately.");
        }

        [TestMethod]
        public async Task StartDiscoveringAsync_SetsIsDiscovering_WithoutSettingIsAdvertising()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);

            await sut.StartDiscoveringAsync();

            Assert.IsTrue(sut.IsDiscovering);
            Assert.IsFalse(sut.IsAdvertising);
        }

        [TestMethod]
        public async Task StopAdvertisingAsync_ClearsIsAdvertising_AndLeavesDiscoveryRunning()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveringAsync();

            await sut.StopAdvertisingAsync();

            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsTrue(sut.IsDiscovering, "Stopping one must not stop the other.");
        }

        [TestMethod]
        public async Task StopAsync_ClearsBothToggles()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveringAsync();

            await sut.StopAsync();

            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering);
        }

        [TestMethod]
        public async Task StartAdvertisingAsync_CalledTwice_IsNoOp()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);

            await sut.StartAdvertisingAsync();
            await sut.StartAdvertisingAsync();

            Assert.IsTrue(sut.IsAdvertising);
            Assert.AreEqual(1, connections.AdvertiseCallCount, "A second start must not reach the platform again.");
        }
    }

    // -------------------------------------------------------------------------
    // Start failures. Previously delivered as a faulted stream observed at first
    // enumeration; the pump now reports them and clears the toggle.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class StartFailures : NearbySessionTests
    {
        [TestMethod]
        public async Task AdvertisePumpFailure_ClearsIsAdvertising()
        {
            var connections = new FakeNearbyConnections
            {
                AdvertiseFault = new NearbyAdvertisingException("radio off"),
            };
            var sut = CreateSut(connections);

            await sut.StartAdvertisingAsync();
            await connections.WaitForAdvertisePumpAsync();

            Assert.IsFalse(sut.IsAdvertising, "A failed start must not leave the session claiming to advertise.");
        }

        [TestMethod]
        public async Task DiscoverPumpFailure_ClearsIsDiscovering()
        {
            var connections = new FakeNearbyConnections
            {
                DiscoverFault = new NearbyDiscoveryException("permission denied"),
            };
            var sut = CreateSut(connections);

            await sut.StartDiscoveringAsync();
            await connections.WaitForDiscoverPumpAsync();

            Assert.IsFalse(sut.IsDiscovering);
        }
    }

    // -------------------------------------------------------------------------
    // Discovery projected onto the Devices collection.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Discovery : NearbySessionTests
    {
        [TestMethod]
        public async Task DeviceFound_AddsToDevices()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            Assert.HasCount(1, sut.Devices);
            Assert.AreSame(device, sut.Devices[0]);
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        [TestMethod]
        public async Task DeviceFound_Twice_DoesNotDuplicate()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            await connections.EmitDeviceFoundAsync(device);

            Assert.HasCount(1, sut.Devices);
        }

        [TestMethod]
        public async Task DeviceLost_RemovesVisibleDevice()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            await connections.EmitDeviceLostAsync(device);

            Assert.IsEmpty(sut.Devices);
        }

        [TestMethod]
        public async Task DeviceLost_DoesNotRemoveConnectedDevice()
        {
            // Going out of discovery range is not the same as disconnecting. Removing a connected
            // device here would delete a live conversation from the UI.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await connections.EmitDeviceLostAsync(device);

            Assert.HasCount(1, sut.Devices);
            Assert.AreEqual(NearbyDeviceStatus.Connected, device.Status);
        }

        [TestMethod]
        public async Task StopDiscoveringAsync_DrainsVisibleDevices()
        {
            // Otherwise the UI shows devices that are no longer being looked for, forever.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-1", "Alice"));
            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-2", "Bob"));

            await sut.StopDiscoveringAsync();

            Assert.IsEmpty(sut.Devices);
        }

        [TestMethod]
        public async Task StopDiscoveringAsync_KeepsConnectedDevices()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await sut.StopDiscoveringAsync();

            Assert.HasCount(1, sut.Devices);
        }
    }

    // -------------------------------------------------------------------------
    // Inbound requests: accept / reject.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class InboundRequests : NearbySessionTests
    {
        [TestMethod]
        public async Task RequestArriving_RaisesConnectionRequested_AndSurfacesDevice()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            NearbyConnectionRequestedEventArgs? captured = null;
            sut.ConnectionRequested += (_, e) => captured = e;

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => CreateConnection(device));

            Assert.IsNotNull(captured);
            Assert.AreSame(device, captured.Device);
            Assert.AreEqual(NearbyDeviceStatus.RequestReceived, device.Status);
            Assert.Contains(device, sut.Devices, "The device must be in Devices before the event is raised.");
        }

        [TestMethod]
        public async Task AcceptAsync_ConnectsAndRaisesConnectionEstablished()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            var connection = CreateConnection(device);
            await connections.EmitRequestAsync(device, () => connection);

            NearbyConnectionChangedEventArgs? established = null;
            sut.ConnectionEstablished += (_, e) => established = e;

            var result = await sut.AcceptAsync(device);

            Assert.AreSame(connection, result);
            Assert.AreEqual(NearbyDeviceStatus.Connected, device.Status);
            Assert.AreSame(connection, device.Connection);
            Assert.AreEqual(ConnectionRole.Acceptor, device.Role);
            Assert.IsNotNull(established);
            Assert.AreSame(device, established.Device);
        }

        [TestMethod]
        public async Task RejectAsync_DoesNotConnect()
        {
            // Security-relevant: rejecting must never produce a connection.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => CreateConnection(device));

            var established = 0;
            sut.ConnectionEstablished += (_, _) => established++;

            await sut.RejectAsync(device);

            Assert.AreEqual(0, established);
            Assert.IsNull(device.Connection);
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        [TestMethod]
        public async Task AcceptAsync_AfterReject_Throws()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => CreateConnection(device));
            await sut.RejectAsync(device);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.AcceptAsync(device));
        }

        [TestMethod]
        public async Task AcceptAsync_WithNoOutstandingRequest_Throws()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => sut.AcceptAsync(new NearbyDevice("peer-1", "Alice")));
        }

        [TestMethod]
        public async Task AcceptAsync_WhenPlatformFails_ResetsDeviceToVisible()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(
                device,
                () => throw new InvalidOperationException("handshake failed"));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.AcceptAsync(device));

            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status, "A failed handshake must not strand the row on Connecting.");
            Assert.IsNull(device.Role);
        }
    }

    // -------------------------------------------------------------------------
    // Outbound connect.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Connect : NearbySessionTests
    {
        [TestMethod]
        public async Task ConnectAsync_SetsConnectedStateAndRaisesEvent()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = CreateConnection(device);
            connections.ConnectResult = connection;

            NearbyConnectionChangedEventArgs? established = null;
            sut.ConnectionEstablished += (_, e) => established = e;

            var result = await sut.ConnectAsync(device);

            Assert.AreSame(connection, result);
            Assert.AreEqual(NearbyDeviceStatus.Connected, device.Status);
            Assert.AreEqual(ConnectionRole.Initiator, device.Role);
            Assert.IsNotNull(established);
        }

        [TestMethod]
        public async Task ConnectAsync_WhenRejected_ResetsDeviceToVisible()
        {
            var connections = new FakeNearbyConnections
            {
                ConnectFault = new InvalidOperationException("rejected"),
            };
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.ConnectAsync(device));

            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
            Assert.IsNull(device.Role);
            Assert.IsNull(device.Connection);
        }

        [TestMethod]
        public async Task ConnectAsync_NullDevice_Throws()
        {
            var sut = CreateSut(new FakeNearbyConnections());

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => sut.ConnectAsync(null!));
        }

        // The regression this guards: a consumer constructed after the connection opens never
        // subscribes, never starts a receive loop, and loses every inbound payload with no error
        // anywhere. The warning is the only signal, so its absence is itself the bug.
        [TestMethod]
        public async Task ConnectAsync_WithNoConnectionEstablishedSubscribers_LogsWarning()
        {
            var logger = new CapturingLogger();
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections, logger);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            await sut.ConnectAsync(device);

            var warning = logger.Records.SingleOrDefault(r =>
                r.Level == LogLevel.Warning && r.Message.Contains("ConnectionEstablished", StringComparison.Ordinal));

            Assert.IsNotNull(
                warning.Message,
                "A connection with no ConnectionEstablished subscriber silently discards every inbound payload; it must warn.");
            Assert.Contains("peer-1", warning.Message, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task ConnectAsync_WithSubscriber_DoesNotLogWarning()
        {
            var logger = new CapturingLogger();
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections, logger);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            sut.ConnectionEstablished += (_, _) => { };

            await sut.ConnectAsync(device);

            Assert.IsFalse(
                logger.Records.Any(r => r.Level == LogLevel.Warning),
                "A correctly wired consumer must not be warned — a guardrail that cries wolf gets filtered out.");
        }
    }

    // -------------------------------------------------------------------------
    // Disconnect. Guards P2-3: the drop must be reported exactly once, whichever
    // side ended it.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Disconnect : NearbySessionTests
    {
        [TestMethod]
        public async Task RemoteDisconnect_RaisesConnectionDroppedExactlyOnce()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = CreateConnection(device);
            connections.ConnectResult = connection;

            var dropped = 0;
            sut.ConnectionDropped += (_, _) => dropped++;

            await sut.ConnectAsync(device);
            await connection.DisposeAsync();
            await WaitForAsync(() => dropped > 0);

            Assert.AreEqual(1, dropped, "A duplicate drop was a previously fixed bug (P2-3).");
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
            Assert.IsNull(device.Connection);
        }

        [TestMethod]
        public async Task DisconnectAsync_RaisesConnectionDroppedExactlyOnce()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            var dropped = 0;
            sut.ConnectionDropped += (_, _) => dropped++;

            await sut.ConnectAsync(device);
            await sut.DisconnectAsync(device);
            await WaitForAsync(() => dropped > 0);

            Assert.AreEqual(1, dropped, "A local disconnect must be reported exactly like a remote one.");
            Assert.IsNull(device.Connection);
        }

        [TestMethod]
        public async Task DisconnectAsync_WhenNotConnected_IsNoOp()
        {
            var sut = CreateSut(new FakeNearbyConnections());
            var device = new NearbyDevice("peer-1", "Alice");

            await sut.DisconnectAsync(device);

            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        [TestMethod]
        public async Task DisconnectAsync_LeavesOtherConnectionsIntact()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);

            var alice = new NearbyDevice("peer-1", "Alice");
            var bob = new NearbyDevice("peer-2", "Bob");

            connections.ConnectResult = CreateConnection(alice);
            await sut.ConnectAsync(alice);
            connections.ConnectResult = CreateConnection(bob);
            await sut.ConnectAsync(bob);

            await sut.DisconnectAsync(alice);

            Assert.AreEqual(NearbyDeviceStatus.Connected, bob.Status);
            Assert.IsNotNull(bob.Connection);
        }
    }

    // -------------------------------------------------------------------------
    // Payload delivery — the stream survives untouched by this restructure.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Payloads : NearbySessionTests
    {
        [TestMethod]
        public async Task PayloadWrittenBeforeDisconnect_IsNotLost()
        {
            // The hardest-won guarantee in the payload path: completing the writer ends the loop,
            // but anything already buffered must still be delivered.
            var device = new NearbyDevice("peer-1", "Alice");
            var channel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = CreateConnection(device, channel);

            channel.Writer.TryWrite(new BytesPayload([1, 2, 3]));
            await connection.DisposeAsync();

            var received = new List<NearbyPayload>();

            await foreach (var payload in connection.ReceiveAsync())
            {
                received.Add(payload);
            }

            Assert.HasCount(1, received);
        }

        [TestMethod]
        public async Task PayloadsFromMultipleConnections_AllArrive()
        {
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);

            var alice = new NearbyDevice("peer-1", "Alice");
            var bob = new NearbyDevice("peer-2", "Bob");

            var aliceChannel = Channel.CreateUnbounded<NearbyPayload>();
            var bobChannel = Channel.CreateUnbounded<NearbyPayload>();

            connections.ConnectResult = CreateConnection(alice, aliceChannel);
            var aliceConnection = await sut.ConnectAsync(alice);
            connections.ConnectResult = CreateConnection(bob, bobChannel);
            var bobConnection = await sut.ConnectAsync(bob);

            aliceChannel.Writer.TryWrite(new BytesPayload([1]));
            bobChannel.Writer.TryWrite(new BytesPayload([2]));
            aliceChannel.Writer.TryComplete();
            bobChannel.Writer.TryComplete();

            var aliceCount = 0;
            var bobCount = 0;

            await foreach (var _ in aliceConnection.ReceiveAsync())
            {
                aliceCount++;
            }

            await foreach (var _ in bobConnection.ReceiveAsync())
            {
                bobCount++;
            }

            Assert.AreEqual(1, aliceCount);
            Assert.AreEqual(1, bobCount);
        }
    }

    // -------------------------------------------------------------------------
    // Hazards flagged by the test-mining pass.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Hazards : NearbySessionTests
    {
        [TestMethod]
        public async Task EnumeratingDevices_WhileCollectionMutates_DoesNotThrow()
        {
            // Ports the *hazard* behind ConnectionLifecycleAdversarialTests rather than the test:
            // the specific bug died with ConnectionLifecycle, but handing consumers a live
            // collection makes "collection was modified during enumeration" newly reachable.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

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
                _ = sut.Devices.ToArray().Length;
            }

            await mutating;

            Assert.IsGreaterThanOrEqualTo(150, sut.Devices.Count);
        }

        [TestMethod]
        public async Task ThrowingEventHandler_DoesNotBreakTheSession()
        {
            // C# events run handlers synchronously; without a guard, one bad consumer handler would
            // take down the platform callback path.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            sut.ConnectionEstablished += (_, _) => throw new InvalidOperationException("bad handler");

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            var connection = await sut.ConnectAsync(device);

            Assert.IsNotNull(connection);
            Assert.AreEqual(NearbyDeviceStatus.Connected, device.Status);
        }

        [TestMethod]
        public async Task SubscribeUnsubscribeCycles_LeaveHandlerFiringExactlyOnce()
        {
            // R-6, the one way this restructure could make a consumer worse. The old
            // EventsAsync(NavigationToken) streams cleaned up by ending their enumeration; C# events
            // against a singleton do not. Simulates five enter/leave page visits: a handler attached
            // without a matching detach would fire five times per event.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var calls = 0;
            void Handler(object? sender, NearbyConnectionChangedEventArgs e) => calls++;

            for (var visit = 0; visit < 5; visit++)
            {
                sut.ConnectionEstablished += Handler;
                sut.ConnectionEstablished -= Handler;
            }

            // Sixth visit: still on the page when the event fires.
            sut.ConnectionEstablished += Handler;

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            Assert.AreEqual(1, calls, "Handlers must fire once per event, not once per page visit.");
        }

        [TestMethod]
        public async Task StopAsync_RejectsOutstandingRequests()
        {
            // Otherwise the remote device waits on a request nobody will ever answer.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            var rejected = false;
            await connections.EmitRequestAsync(
                device,
                () => CreateConnection(device),
                onReject: () => rejected = true);

            await sut.StopAsync();

            Assert.IsTrue(rejected);
            Assert.IsEmpty(sut.Devices);
        }
    }

    // -------------------------------------------------------------------------
    // Backgrounding teardown (docs/TESTING-AND-LIFECYCLE-PLAN.md §3.6/§3.7).
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
    public sealed class BackgroundTeardown : NearbySessionTests
    {
        [TestMethod]
        public async Task StopAsync_RaisesConnectionDropped_ForEveryLiveConnection()
        {
            // The zombie-Connected bug: without this, a consumer backgrounded mid-conversation
            // is never told the connection ended, because iOS tears MPC down silently and with
            // no NSError. ConnectionDropped is the only signal it will ever get.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            var dropped = new List<string>();
            sut.ConnectionDropped += (_, e) => dropped.Add(e.Device.Id);

            await sut.ConnectAsync(device);
            Assert.AreEqual(NearbyDeviceStatus.Connected, device.Status);

            await sut.StopAsync();
            await WaitForAsync(() => dropped.Count > 0);

            Assert.HasCount(1, dropped);
            Assert.AreEqual("peer-1", dropped[0]);
        }

        [TestMethod]
        public async Task StopAsync_ClearsConnectedState_SoNoDeviceIsLeftReportingConnected()
        {
            // Devices is the state consumers bind to. A row still reading Connected after the OS
            // ended the session is precisely the state this fix exists to eliminate.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await sut.StopAsync();

            Assert.IsEmpty(sut.Devices);

            // Awaited, not asserted outright: the per-device clear happens in WatchDisconnectAsync,
            // which observes the connection's Disconnected task and therefore lands after StopAsync
            // returns. See StopAsync_ClearingDeviceState_IsNotSynchronous for why that ordering is
            // load-bearing on iOS rather than an incidental detail.
            await WaitForAsync(() => device.Connection is null);

            Assert.IsNull(device.Connection, "A cleared device must not still hold a dead connection.");
            Assert.AreNotEqual(NearbyDeviceStatus.Connected, device.Status);
        }

        [TestMethod]
        public async Task StopAsync_ClearingDeviceState_IsNotSynchronous()
        {
            // Documents a real hazard rather than asserting desired behaviour. StopAsync clears the
            // Devices collection synchronously, but per-device state (Connection, Status) is cleared
            // by WatchDisconnectAsync, which runs as a continuation on the connection's Disconnected
            // task. So immediately after StopAsync returns, a device the caller still holds a
            // reference to can briefly report Connected with a live Connection.
            //
            // Consumers binding to Devices never see this — the row is already gone. It matters on
            // iOS: AppLifecycleObserver cannot await teardown (UIKit gives seconds before
            // suspension), so the process may suspend before this continuation runs. That is
            // acceptable because iOS has already destroyed the transport and the state is rebuilt
            // from scratch on foreground — but if this ever needs to be synchronous, this test is
            // the one that will fail and say why.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await sut.StopAsync();

            Assert.IsEmpty(sut.Devices, "The collection consumers bind to is cleared synchronously.");

            await WaitForAsync(() => device.Connection is null);
            Assert.IsNull(device.Connection, "Per-device state is cleared, just asynchronously.");
        }

        [TestMethod]
        public async Task StopAsync_ClearsBothToggles_SoNeitherReportsScanningWhileSuspended()
        {
            // The second zombie state. While suspended nothing is advertising or scanning, so
            // leaving these true would misreport the radio just as Connected misreported the session.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveringAsync();

            await sut.StopAsync();

            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering);
        }

        [TestMethod]
        public async Task StopAsync_LeavesSessionReusable_SoTheAppCanStartAgainOnForeground()
        {
            // Nothing restarts automatically: the app calls Start* again on foreground. That is
            // only viable if StopAsync leaves the session usable rather than terminally torn down.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartDiscoveringAsync();

            await sut.StopAsync();
            Assert.IsFalse(sut.IsDiscovering);

            await sut.StartDiscoveringAsync();

            Assert.IsTrue(sut.IsDiscovering);
            Assert.AreEqual(2, connections.DiscoverCallCount, "Restart must reach the platform, not be swallowed as a no-op.");
        }

        [TestMethod]
        public async Task StopAsync_IsIdempotent_SoARepeatedBackgroundNotificationIsHarmless()
        {
            // DidEnterBackground can arrive more than once across a suspend/resume cycle, and the
            // observer does not deduplicate — it relies on StopAsync being safe to call again.
            var connections = new FakeNearbyConnections();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveringAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            var dropped = 0;
            sut.ConnectionDropped += (_, _) => dropped++;

            await sut.StopAsync();
            await sut.StopAsync();
            await sut.StopAsync();

            await WaitForAsync(() => dropped > 0);

            Assert.AreEqual(1, dropped, "ConnectionDropped must be raised once per connection, not once per StopAsync call.");
            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering);
        }
    }

    static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
