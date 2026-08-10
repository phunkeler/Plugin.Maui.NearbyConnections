using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Behavioural tests for <see cref="NearbyImplementation"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces <c>NearbyAdvertiserTests</c> + <c>NearbyDiscovererTests</c> (2,435 lines of two
/// near-identical mirrors). Those files asserted the mechanics of the event-union/broadcaster design
/// that no longer exists; what survives here is the consumer-visible behaviour they guarded, stated
/// once.
/// </para>
/// <para>
/// The session takes <see cref="IPlatformNearby"/> rather than the concrete implementation
/// precisely so these can run on <c>net10.0</c>, where every <c>Platform*</c> start throws.
/// </para>
/// </remarks>
[TestCategory("Session")]
public class NearbySessionTests
{
    static NearbyImplementation CreateSut(FakeNearby connections, NearbyOptions? options = null)
        => new(connections, options ?? new NearbyOptions(), NullLogger.Instance);

    static NearbyImplementation CreateSut(FakeNearby connections, ILogger logger)
        => new(connections, new NearbyOptions(), logger);

    /// <summary>
    /// The session's current snapshot of a device. A <see cref="NearbyDevice"/> handed to the
    /// session is an immutable value, so the local variable a test holds never updates — every
    /// status assertion has to re-read through <see cref="INearby.Devices"/>.
    /// </summary>
    static NearbyDevice? Current(INearby session, string deviceId)
        => session.Devices.FirstOrDefault(d => d.Id == deviceId);

    static NearbyDeviceStatus? StatusOf(INearby session, string deviceId)
        => Current(session, deviceId)?.Status;

    /// <summary>
    /// Records everything published to <see cref="INearbyDevices.Changes"/>, so a test can assert on
    /// transitions the way it used to assert on the lifecycle events this replaced.
    /// </summary>
    /// <remarks>
    /// Constructing one subscribes immediately, so a change raised after this returns is always
    /// captured — the ordering the removed events could not guarantee.
    /// </remarks>
    sealed class ChangeRecorder : IAsyncDisposable
    {
        readonly List<NearbyDeviceChange> _changes = [];
        readonly CancellationTokenSource _cts = new();
        readonly IAsyncEnumerator<NearbyDeviceChange> _enumerator;
        readonly Task _pump;

        public ChangeRecorder(INearby session)
        {
            _enumerator = session.Devices.Changes.GetAsyncEnumerator(_cts.Token);

            // Kick the enumerator here, synchronously, so the watcher's channel is registered
            // before the constructor returns. Starting it inside PumpAsync instead leaves a window
            // where a change published immediately after construction is never seen — the pump has
            // not reached its first MoveNextAsync yet, so nothing is subscribed to receive it.
            var first = _enumerator.MoveNextAsync();
            _pump = PumpAsync(first);
        }

        public IReadOnlyList<NearbyDeviceChange> Changes
        {
            get { lock (_changes) { return [.. _changes]; } }
        }

        /// <summary>Every change recorded for one device, oldest first.</summary>
        public IReadOnlyList<NearbyDeviceChange> For(string deviceId)
            => [.. Changes.Where(c => c.Device.Id == deviceId)];

        /// <summary>
        /// Waits until at least <paramref name="count"/> changes have been recorded for a device.
        /// Publishing hands the change to a channel and the pump drains it on another thread, so an
        /// assertion made immediately after an operation can otherwise race the recording.
        /// </summary>
        public Task WaitForAsync(string deviceId, int count)
            => NearbySessionTests.WaitForAsync(() => For(deviceId).Count >= count);

        /// <summary>The statuses a device has been reported in, oldest first.</summary>
        public IReadOnlyList<NearbyDeviceStatus> StatusesFor(string deviceId)
            => [.. For(deviceId).Select(c => c.Device.Status)];

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();

            try
            {
                await _pump;
            }
            catch (OperationCanceledException)
            {
                // Expected: cancelling is how the pump is stopped.
            }

            _cts.Dispose();
        }

        async Task PumpAsync(ValueTask<bool> first)
        {
            try
            {
                var hasNext = await first;

                while (hasNext)
                {
                    lock (_changes)
                    {
                        _changes.Add(_enumerator.Current);
                    }

                    hasNext = await _enumerator.MoveNextAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on disposal.
            }
            finally
            {
                await _enumerator.DisposeAsync();
            }
        }
    }

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
            sendBytes: (_, _) => ValueTask.CompletedTask,
            sendFile: (_, _, _) => Task.CompletedTask,
            dispose: () => ValueTask.CompletedTask);

    // -------------------------------------------------------------------------
    // Preflight availability
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class CheckAvailability : NearbySessionTests
    {
        [TestMethod]
        public async Task Always_DelegatesToThePlatform()
        {
            var connections = new FakeNearby { Availability = NearbyAvailability.Ready };
            var sut = CreateSut(connections);

            var result = await sut.CheckAvailabilityAsync();

            Assert.AreEqual(NearbyAvailability.Ready, result);
            Assert.AreEqual(1, connections.CheckAvailabilityCallCount);
        }

        [TestMethod]
        public async Task MultipleProblems_AreReportedTogether()
        {
            // The whole reason this is a [Flags] enum: a user with Bluetooth off AND permissions
            // denied should be told both at once, not made to fix one and retry to discover the
            // other.
            var connections = new FakeNearby
            {
                Availability = NearbyAvailability.BluetoothDisabled | NearbyAvailability.MissingPermissions,
        };
            var sut = CreateSut(connections);

            var result = await sut.CheckAvailabilityAsync();

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
            var connections = new FakeNearby
            {
                Availability = NearbyAvailability.MissingPermissions,
        };
            var sut = CreateSut(connections);

            var result = await sut.CheckAvailabilityAsync();

            Assert.AreNotEqual(NearbyAvailability.Ready, result);
        }

        [TestMethod]
        public async Task DoesNotStartAdvertisingOrDiscovery()
        {
            // A preflight check must not have side effects: it reports state, it does not mutate it.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);

            await sut.CheckAvailabilityAsync();

            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering);
            Assert.AreEqual(0, connections.AdvertiseCallCount);
            Assert.AreEqual(0, connections.DiscoverCallCount);
        }

        [TestMethod]
        public async Task CanceledToken_Throws()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => sut.CheckAvailabilityAsync(cts.Token));
        }
    }

    // -------------------------------------------------------------------------
    // Advertising / discovery toggles — decision 8: they are independent.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Toggles : NearbySessionTests
    {
        [TestMethod]
        public async Task StartAdvertisingAsync_SetsIsAdvertising_WithoutSettingIsDiscovering()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);

            await sut.StartAdvertisingAsync();

            Assert.IsTrue(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering, "Advertising must not imply discovering — both sample pages toggle them separately.");
        }

        [TestMethod]
        public async Task StartDiscoveryAsync_SetsIsDiscovering_WithoutSettingIsAdvertising()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);

            await sut.StartDiscoveryAsync();

            Assert.IsTrue(sut.IsDiscovering);
            Assert.IsFalse(sut.IsAdvertising);
        }

        [TestMethod]
        public async Task StopAdvertisingAsync_ClearsIsAdvertising_AndLeavesDiscoveryRunning()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveryAsync();

            await sut.StopAdvertisingAsync();

            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsTrue(sut.IsDiscovering, "Stopping one must not stop the other.");
        }

        [TestMethod]
        public async Task StopAsync_ClearsBothToggles()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveryAsync();

            await sut.StopAsync();

            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering);
        }

        [TestMethod]
        public async Task StartAdvertisingAsync_CalledTwice_IsNoOp()
        {
            var connections = new FakeNearby();
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
            var connections = new FakeNearby
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
            var connections = new FakeNearby
            {
                DiscoverFault = new NearbyDiscoveryException("permission denied"),
        };
            var sut = CreateSut(connections);

            await sut.StartDiscoveryAsync();
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
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            Assert.HasCount(1, sut.Devices);
            Assert.AreSame(device, sut.Devices[0]);
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        [TestMethod]
        public async Task DeviceFound_Twice_DoesNotDuplicate()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            await connections.EmitDeviceFoundAsync(device);

            Assert.HasCount(1, sut.Devices);
        }

        [TestMethod]
        public async Task DeviceLost_RemovesVisibleDevice()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

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
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);

            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await connections.EmitDeviceLostAsync(device);

            Assert.HasCount(1, sut.Devices);
            Assert.AreEqual(NearbyDeviceStatus.Connected, StatusOf(sut, "peer-1"));
        }

        [TestMethod]
        public async Task StopDiscoveryAsync_DrainsVisibleDevices()
        {
            // Otherwise the UI shows devices that are no longer being looked for, forever.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-1", "Alice"));
            await connections.EmitDeviceFoundAsync(new NearbyDevice("peer-2", "Bob"));

            await sut.StopDiscoveryAsync();

            Assert.IsEmpty(sut.Devices);
        }

        [TestMethod]
        public async Task StopDiscoveryAsync_KeepsConnectedDevices()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitDeviceFoundAsync(device);
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await sut.StopDiscoveryAsync();

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
        public async Task RequestArriving_ReportsRequestReceived_AndSurfacesDevice()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            await using var recorder = new ChangeRecorder(sut);

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => CreateConnection(device));

            await recorder.WaitForAsync("peer-1", 2);

            Assert.AreEqual(NearbyDeviceStatus.RequestReceived, StatusOf(sut, "peer-1"));
            Assert.Contains(device, sut.Devices);

            // Added before Updated: a consumer must never see a status change for a device it has
            // not been told about.
            Assert.AreSequenceEqual(
                new[] { NearbyDeviceChangeAction.Added, NearbyDeviceChangeAction.Updated },
                recorder.For("peer-1").Select(c => c.Action).ToArray());
        }

        [TestMethod]
        public async Task AutoAccept_ConnectsWithoutEverReportingRequestReceived()
        {
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var sut = CreateSut(connections, options);
            await sut.StartAdvertisingAsync();

            await using var recorder = new ChangeRecorder(sut);
            var device = new NearbyDevice("peer-1", "Alice");

            await connections.EmitRequestAsync(device, () => CreateConnection(device));

            await recorder.WaitForAsync("peer-1", 2);

            Assert.AreEqual(NearbyDeviceStatus.Connected, StatusOf(sut, "peer-1"));

            // The documented contract of auto-accept: the state is skipped, not merely unreported.
            Assert.DoesNotContain(
                NearbyDeviceStatus.RequestReceived,
                recorder.StatusesFor("peer-1"),
                "RequestReceived must not be observable when requests are auto-accepted.");
        }

        [TestMethod]
        public async Task AutoAccept_LeavesNoPendingRequestToAnswer()
        {
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var sut = CreateSut(connections, options);
            await sut.StartAdvertisingAsync();
            var device = new NearbyDevice("peer-1", "Alice");

            await connections.EmitRequestAsync(device, () => CreateConnection(device));

            // The session already answered, so there is nothing left for the application to accept.
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.AcceptAsync(device));
        }

        [TestMethod]
        public async Task AutoAccept_WhenAcceptFails_ResetsDeviceToVisible()
        {
            var connections = new FakeNearby();
            var options = new NearbyOptions { AutoAcceptConnectionRequests = true };
            var sut = CreateSut(connections, options);
            await sut.StartAdvertisingAsync();
            var device = new NearbyDevice("peer-1", "Alice");

            await connections.EmitRequestAsync(
                device,
                () => throw new NearbyException("Handshake failed."));

            // A failed auto-accept must not strand the row on Connecting, and must not escape into
            // the advertise pump and stop advertising.
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
            Assert.IsTrue(sut.IsAdvertising);
        }

        [TestMethod]
        public async Task AcceptAsync_ConnectsAndReportsConnected()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            var connection = CreateConnection(device);
            await connections.EmitRequestAsync(device, () => connection);

            await using var recorder = new ChangeRecorder(sut);

            var result = await sut.AcceptAsync(device);

            Assert.AreSame(connection, result);
            Assert.AreEqual(NearbyDeviceStatus.Connected, StatusOf(sut, "peer-1"));
            Assert.AreEqual(ConnectionRole.Acceptor, Current(sut, "peer-1")!.Role);
            Assert.IsTrue(sut.TryGetConnection(device.Id, out var lookedUp));
            Assert.AreSame(connection, lookedUp);

            await recorder.WaitForAsync("peer-1", 1);
            Assert.Contains(NearbyDeviceStatus.Connected, recorder.StatusesFor("peer-1"));
        }

        [TestMethod]
        public async Task RejectAsync_DoesNotConnect()
        {
            // Security-relevant: rejecting must never produce a connection.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(device, () => CreateConnection(device));

            await using var recorder = new ChangeRecorder(sut);

            await sut.RejectAsync(device);

            Assert.DoesNotContain(NearbyDeviceStatus.Connected, recorder.StatusesFor("peer-1"));
            Assert.IsFalse(sut.TryGetConnection(device.Id, out _));
            Assert.AreEqual(NearbyDeviceStatus.Visible, StatusOf(sut, "peer-1"));
        }

        [TestMethod]
        public async Task AcceptAsync_AfterReject_Throws()
        {
            var connections = new FakeNearby();
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
            var connections = new FakeNearby();
            var sut = CreateSut(connections);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => sut.AcceptAsync(new NearbyDevice("peer-1", "Alice")));
        }

        [TestMethod]
        public async Task AcceptAsync_WhenPlatformFails_ResetsDeviceToVisible()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            await connections.EmitRequestAsync(
                device,
                () => throw new InvalidOperationException("handshake failed"));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.AcceptAsync(device));

            Assert.AreEqual(NearbyDeviceStatus.Visible, StatusOf(sut, "peer-1"), "A failed handshake must not strand the row on Connecting.");
        }
    }

    // -------------------------------------------------------------------------
    // Outbound connect.
    // -------------------------------------------------------------------------

    [TestClass]
    public sealed class Connect : NearbySessionTests
    {
        [TestMethod]
        public async Task ConnectAsync_SetsConnectedStateAndReportsIt()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = CreateConnection(device);
            connections.ConnectResult = connection;

            await using var recorder = new ChangeRecorder(sut);

            var result = await sut.ConnectAsync(device);

            Assert.AreSame(connection, result);
            Assert.AreEqual(NearbyDeviceStatus.Connected, StatusOf(sut, "peer-1"));
            Assert.AreEqual(ConnectionRole.Initiator, Current(sut, "peer-1")!.Role);
            Assert.IsTrue(sut.TryGetConnection(device.Id, out _));

            await recorder.WaitForAsync("peer-1", 1);
            Assert.Contains(
                NearbyDeviceStatus.Connected,
                recorder.StatusesFor("peer-1"),
                "Reaching Connected must be reported as a change, not only reflected in Devices.");
        }

        [TestMethod]
        public async Task ConnectAsync_WhenRejected_ResetsDeviceToVisible()
        {
            var connections = new FakeNearby
            {
                ConnectFault = new InvalidOperationException("rejected"),
        };
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.ConnectAsync(device));

            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        [TestMethod]
        public async Task ConnectAsync_NullDevice_Throws()
        {
            var sut = CreateSut(new FakeNearby());

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => sut.ConnectAsync(null!));
        }

        // NOTE: two tests were removed here with the ConnectionEstablished event. They asserted a
        // warning logged when a connection opened with nobody subscribed — the signal for a
        // consumer constructed too late to start a receive loop, which silently loses every
        // inbound payload. A broadcast change stream has no subscriber count to check, so that
        // guardrail is not expressible and the warning is gone. PlatformNearby still warns once
        // per connection when a payload arrives and ReceiveAsync was never called, which catches
        // the same mistake one step later. See docs/PAYLOAD-DELIVERY.md.
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
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            var connection = CreateConnection(device);
            connections.ConnectResult = connection;

            await sut.ConnectAsync(device);

            await using var recorder = new ChangeRecorder(sut);

            await connection.DisposeAsync();
            await WaitForAsync(() => StatusOf(sut, "peer-1") is NearbyDeviceStatus.Visible);

            Assert.HasCount(
                1,
                recorder.StatusesFor("peer-1"),
                "A duplicate drop was a previously fixed bug (P2-3).");
            Assert.AreEqual(NearbyDeviceStatus.Visible, StatusOf(sut, "peer-1"));
        }

        [TestMethod]
        public async Task DisconnectAsync_ReportsTheDropExactlyOnce()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            await sut.ConnectAsync(device);

            await using var recorder = new ChangeRecorder(sut);

            await sut.DisconnectAsync(device);
            await WaitForAsync(() => StatusOf(sut, "peer-1") is NearbyDeviceStatus.Visible);

            Assert.HasCount(
                1,
                recorder.StatusesFor("peer-1"),
                "A local disconnect must be reported exactly like a remote one.");
        }

        [TestMethod]
        public async Task DisconnectAsync_WhenNotConnected_IsNoOp()
        {
            var sut = CreateSut(new FakeNearby());
            var device = new NearbyDevice("peer-1", "Alice");

            await sut.DisconnectAsync(device);

            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
        }

        // GAP: the reason a connection ended is no longer observable by consumers. It travelled on
        // NearbyConnectionChangedEventArgs.Reason, and a device back in Visible carries no reason.
        // EndReason now reaches logs only. This test asserts what a consumer CAN still see, and
        // exists to be rewritten when a reason is reattached to the transition.
        [TestMethod]
        public async Task Disconnect_ReturnsDeviceToVisible()
        {
            // Arrange
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            // Act
            await sut.ConnectAsync(device);
            await sut.DisconnectAsync(device);
            await WaitForAsync(() => StatusOf(sut, "peer-1") is NearbyDeviceStatus.Visible);

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, StatusOf(sut, "peer-1"));
            Assert.IsNull(Current(sut, "peer-1")!.Role, "A disconnected device plays no role.");
        }

        // DiscoveryPageViewModel filters on "Visible or Connecting". A device left in any other
        // state after a drop silently vanishes from the discovery list — a capability regression
        // that no other test in this suite would catch.
        [TestMethod]
        public async Task DroppedDevice_RejoinsTheDiscoveryFilter()
        {
            // Arrange
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            // Act
            await sut.ConnectAsync(device);
            await sut.DisconnectAsync(device);
            await WaitForAsync(() => StatusOf(sut, "peer-1") is not NearbyDeviceStatus.Connected);

            // Assert
            Assert.IsTrue(
                StatusOf(sut, "peer-1") is NearbyDeviceStatus.Visible or NearbyDeviceStatus.Connecting,
                $"A dropped device must rejoin the discovery filter, but was {StatusOf(sut, "peer-1")}.");
        }

        // Cancelled, TimedOut and Failed are branched per exception type rather than defaulted, so
        // a caller that withdraws is not reported as a failure.
        [TestMethod]
        public async Task ConnectAsync_Cancelled_LeavesDeviceVisible()
        {
            // Arrange
            var connections = new FakeNearby { ConnectFault = new OperationCanceledException() };
            var sut = CreateSut(connections);
            var device = new NearbyDevice("peer-1", "Alice");

            // Act
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => sut.ConnectAsync(device));

            // Assert
            Assert.AreEqual(
                NearbyDeviceStatus.Visible,
                StatusOf(sut, "peer-1"),
                "A cancelled handshake must not strand the row on Connecting.");
            Assert.IsNull(Current(sut, "peer-1")!.Role, "A device that is not connecting carries no role.");
        }

        [TestMethod]
        public async Task DisconnectAsync_LeavesOtherConnectionsIntact()
        {
            var connections = new FakeNearby();
            var sut = CreateSut(connections);

            var alice = new NearbyDevice("peer-1", "Alice");
            var bob = new NearbyDevice("peer-2", "Bob");

            connections.ConnectResult = CreateConnection(alice);
            await sut.ConnectAsync(alice);
            connections.ConnectResult = CreateConnection(bob);
            await sut.ConnectAsync(bob);

            await sut.DisconnectAsync(alice);

            // Alice's entry is removed on the Disconnected continuation, not inside DisconnectAsync
            // — the same asynchrony documented by StopAsync_ClearingDeviceState_IsNotSynchronous.
            await WaitForAsync(() => !sut.TryGetConnection(alice.Id, out _));

            Assert.AreEqual(NearbyDeviceStatus.Connected, StatusOf(sut, "peer-2"));
            Assert.IsTrue(sut.TryGetConnection(bob.Id, out _), "Disconnecting one device must not tear down the others.");
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

            channel.Writer.TryWrite(new NearbyBytesPayload([1, 2, 3]));
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
            var connections = new FakeNearby();
            var sut = CreateSut(connections);

            var alice = new NearbyDevice("peer-1", "Alice");
            var bob = new NearbyDevice("peer-2", "Bob");

            var aliceChannel = Channel.CreateUnbounded<NearbyPayload>();
            var bobChannel = Channel.CreateUnbounded<NearbyPayload>();

            connections.ConnectResult = CreateConnection(alice, aliceChannel);
            var aliceConnection = await sut.ConnectAsync(alice);
            connections.ConnectResult = CreateConnection(bob, bobChannel);
            var bobConnection = await sut.ConnectAsync(bob);

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
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

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
        public async Task AbandonedWatcher_DoesNotBreakTheSession()
        {
            // The structural win over events: a consumer cannot run code on the callback path, so a
            // broken consumer cannot take the session down with it. Here the watcher simply stops
            // reading — with an unbounded per-watcher channel that must not block the publisher.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            using var abandonedToken = new CancellationTokenSource();
            var abandoned = sut.Devices.Changes.GetAsyncEnumerator(abandonedToken.Token);

            // Start the enumerator so it subscribes, but never await this: the point is a watcher
            // that has a live channel and is not draining it. Awaiting here would block forever —
            // nothing has been published yet — which is a property of the stream, not a defect.
            var neverAwaited = abandoned.MoveNextAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            var connection = await sut.ConnectAsync(device);

            Assert.IsNotNull(connection);
            Assert.AreEqual(NearbyDeviceStatus.Connected, StatusOf(sut, "peer-1"));

            await abandonedToken.CancelAsync();

            try
            {
                await neverAwaited;
            }
            catch (OperationCanceledException)
            {
                // Expected: cancelling is how an abandoned watcher is torn down.
            }

            await abandoned.DisposeAsync();
        }

        [TestMethod]
        public async Task WatchCycles_LeaveTheSurvivingWatcherReceivingExactlyOnce()
        {
            // R-6, the leak class that motivated the whole restructure. Five enter/leave page
            // visits: with events, a subscription without a matching `-=` fired five times per
            // event. Ending the enumeration is now the only cleanup, and it cannot be forgotten —
            // `await using` does it, and so does breaking out of an `await foreach`.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            for (var visit = 0; visit < 5; visit++)
            {
                await using var transient = new ChangeRecorder(sut);
            }

            // Sixth visit: still watching when the change happens.
            await using var recorder = new ChangeRecorder(sut);

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);
            await recorder.WaitForAsync("peer-1", 1);

            Assert.HasCount(
                1,
                recorder.StatusesFor("peer-1").Where(st => st is NearbyDeviceStatus.Connected),
                "Each change must reach a live watcher once, not once per past page visit.");
        }

        [TestMethod]
        public async Task StopAsync_RejectsOutstandingRequests()
        {
            // Otherwise the remote device waits on a request nobody will ever answer.
            var connections = new FakeNearby();
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
    public sealed class BackgroundTeardown : NearbySessionTests
    {
        [TestMethod]
        public async Task StopAsync_RaisesConnectionDropped_ForEveryLiveConnection()
        {
            // The zombie-Connected bug: without this, a consumer backgrounded mid-conversation
            // is never told the connection ended, because iOS tears MPC down silently and with
            // no NSError. The change stream is the only signal it will ever get.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);

            await sut.ConnectAsync(device);
            Assert.AreEqual(NearbyDeviceStatus.Connected, StatusOf(sut, "peer-1"));

            await using var recorder = new ChangeRecorder(sut);

            await sut.StopAsync();
            await WaitForAsync(() => recorder.For("peer-1").Count > 0);

            // Removal is how a stopped session reports the device is gone; the connection going
            // away is what the backgrounded consumer must be able to observe.
            var removals = recorder.For("peer-1")
                .Where(c => c.Action is NearbyDeviceChangeAction.Removed)
                .ToArray();

            Assert.HasCount(1, removals);
            Assert.IsFalse(sut.TryGetConnection("peer-1", out _));
        }

        [TestMethod]
        public async Task StopAsync_ClearsConnectedState_SoNoDeviceIsLeftReportingConnected()
        {
            // Devices is the state consumers bind to. A row still reading Connected after the OS
            // ended the session is precisely the state this fix exists to eliminate.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await sut.StopAsync();

            Assert.IsEmpty(sut.Devices);

            // Awaited, not asserted outright: the per-device clear happens in WatchDisconnectAsync,
            // which observes the connection's Disconnected task and therefore lands after StopAsync
            // returns. See StopAsync_ClearingDeviceState_IsNotSynchronous for why that ordering is
            // load-bearing on iOS rather than an incidental detail.
            await WaitForAsync(() => device.Status is NearbyDeviceStatus.Visible);

            Assert.AreNotEqual(NearbyDeviceStatus.Connected, device.Status);
            Assert.IsFalse(
                sut.TryGetConnection(device.Id, out _),
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
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await sut.StopAsync();

            Assert.IsEmpty(sut.Devices, "The collection consumers bind to is cleared synchronously.");

            await WaitForAsync(() => device.Status is NearbyDeviceStatus.Visible);
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status, "Per-device state is cleared, just asynchronously.");
            Assert.IsFalse(sut.TryGetConnection(device.Id, out _), "The connection lookup is cleared on the same continuation.");
        }

        [TestMethod]
        public async Task StopAsync_ClearsBothToggles_SoNeitherReportsScanningWhileSuspended()
        {
            // The second zombie state. While suspended nothing is advertising or scanning, so
            // leaving these true would misreport the radio just as Connected misreported the session.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveryAsync();

            await sut.StopAsync();

            Assert.IsFalse(sut.IsAdvertising);
            Assert.IsFalse(sut.IsDiscovering);
        }

        [TestMethod]
        public async Task StopAsync_LeavesSessionReusable_SoTheAppCanStartAgainOnForeground()
        {
            // Nothing restarts automatically: the app calls Start* again on foreground. That is
            // only viable if StopAsync leaves the session usable rather than terminally torn down.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartDiscoveryAsync();

            await sut.StopAsync();
            Assert.IsFalse(sut.IsDiscovering);

            await sut.StartDiscoveryAsync();

            Assert.IsTrue(sut.IsDiscovering);
            Assert.AreEqual(2, connections.DiscoverCallCount, "Restart must reach the platform, not be swallowed as a no-op.");
        }

        [TestMethod]
        public async Task StopAsync_IsIdempotent_SoARepeatedBackgroundNotificationIsHarmless()
        {
            // DidEnterBackground can arrive more than once across a suspend/resume cycle, and the
            // observer does not deduplicate — it relies on StopAsync being safe to call again.
            var connections = new FakeNearby();
            var sut = CreateSut(connections);
            await sut.StartAdvertisingAsync();
            await sut.StartDiscoveryAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            connections.ConnectResult = CreateConnection(device);
            await sut.ConnectAsync(device);

            await using var recorder = new ChangeRecorder(sut);

            await sut.StopAsync();
            await sut.StopAsync();
            await sut.StopAsync();

            await WaitForAsync(() => recorder.For("peer-1").Count > 0);

            Assert.HasCount(
                1,
                recorder.For("peer-1").Where(c => c.Action is NearbyDeviceChangeAction.Removed),
                "A device must be reported removed once per connection, not once per StopAsync call.");
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
