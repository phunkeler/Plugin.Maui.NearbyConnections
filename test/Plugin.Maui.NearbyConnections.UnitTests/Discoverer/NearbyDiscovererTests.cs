using System.Threading.Channels;
using NSubstitute;

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
            {
                yield return ev;
            }
        }

        public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
            => ConnectTcs.Task.WaitAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // ---------------------------------------------------------------------------
    // MultiConnectFake — returns pre-built connections in sequence, one per call.
    // ---------------------------------------------------------------------------
    private sealed class MultiConnectFake(params NearbyConnection[] connections) : INearbyConnections
    {
        int _index;

        readonly Channel<NearbyDeviceEvent> _discoverChannel =
            Channel.CreateUnbounded<NearbyDeviceEvent>();

        public void WriteFound(NearbyDevice device)
            => _discoverChannel.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Found));

        public IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(CancellationToken cancellationToken = default)
            => Channel.CreateUnbounded<NearbyConnectionRequest>().Reader.ReadAllAsync(cancellationToken);

        public async IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var ev in _discoverChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return ev;
            }
        }

        public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
            => Task.FromResult(connections[_index++]);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // ---------------------------------------------------------------------------
    // Helper: NSubstitute fake wired to an in-memory discover channel.
    // Used by EventStreamBehavior tests that need to control the channel directly.
    // ---------------------------------------------------------------------------
    static INearbyConnections CreateSubstitute(Channel<NearbyDeviceEvent>? discoverChannel = null)
    {
        var ch = discoverChannel ?? Channel.CreateUnbounded<NearbyDeviceEvent>();
        var inner = Substitute.For<INearbyConnections>();
        inner.DiscoverAsync(Arg.Any<CancellationToken>())
             .Returns(ci => ch.Reader.ReadAllAsync(ci.Arg<CancellationToken>()));
        return inner;
    }

    // ---------------------------------------------------------------------------
    // Helper: create a NearbyConnection with a writable receive channel.
    // ---------------------------------------------------------------------------
    static (NearbyConnection Connection, Channel<NearbyPayload> ReceiveChannel) CreateConnection(
        NearbyDevice? device = null)
    {
        var ch = Channel.CreateUnbounded<NearbyPayload>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var conn = new NearbyConnection(
            device ?? new NearbyDevice("peer-1", "Alice"),
            ch,
            sendBytesFactory: (_, _) => Task.CompletedTask,
            sendFileFactory: (_, _, _) => Task.CompletedTask,
            disposeFactory: () => ValueTask.CompletedTask);
        return (conn, ch);
    }

    // ---------------------------------------------------------------------------
    // Helper: build a Found NearbyDeviceEvent.
    // ---------------------------------------------------------------------------
    static NearbyDeviceEvent Found(string id, string name) =>
        new(new NearbyDevice(id, name), NearbyDeviceEventType.Found);

    // ---------------------------------------------------------------------------
    // Helper: wait with a short polling loop for a condition to become true.
    // ---------------------------------------------------------------------------
    static async Task WaitForAsync(Func<bool> condition, int maxMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.IsTrue(condition(), $"Condition not met within {maxMs} ms.");
    }

    // ---------------------------------------------------------------------------
    // Helper: start discovering, emit a Found event, connect, and return state.
    // ---------------------------------------------------------------------------
    static async Task<(NearbyDiscoverer Discoverer, NearbyConnection Connection, Channel<NearbyPayload> ReceiveChannel)>
        SetupConnectedDiscoverer(FakeNearbyConnections fake, NearbyDevice? device = null)
    {
        var discoverer = new NearbyDiscoverer(fake);
        await discoverer.StartAsync();

        var d = device ?? new NearbyDevice("peer-1", "Alice");
        fake.WriteFound(d);

        // Give the run loop time to process the Found event
        await Task.Delay(50);

        var (conn, ch) = CreateConnection(d);
        fake.ConnectTcs.SetResult(conn);

        await discoverer.ConnectAsync(d);
        return (discoverer, conn, ch);
    }

    // ===========================================================================
    // StartAsync
    // ===========================================================================
    [TestClass]
    public sealed class StartAsync
    {
        [TestMethod]
        public async Task StartAsync_SetsIsDiscovering_True()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);

            // Act
            await discoverer.StartAsync();

            // Assert
            Assert.IsTrue(discoverer.IsDiscovering);

            await discoverer.StopAsync();
        }
    }

    // ===========================================================================
    // StopAsync
    // ===========================================================================
    [TestClass]
    public sealed class StopAsync
    {
        [TestMethod]
        public async Task StopAsync_SetsIsDiscovering_False()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
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
    // EventsAsync
    // ===========================================================================
    [TestClass]
    public sealed class EventsAsync
    {
        [TestMethod]
        public async Task Synchronized_IsFirstEvent_WhenNoState()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            DiscovererEvent? firstEvent = null;

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    firstEvent = ev;
                    break;
                }
            }, cts.Token);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "EventsAsync did not emit Synchronized within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsInstanceOfType<DiscovererEvent.Synchronized>(firstEvent);

            await discoverer.StopAsync();
        }

        [TestMethod]
        public async Task DeviceFound_Event_WhenDeviceDiscovered()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.DeviceFound>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            var device = new NearbyDevice("peer-1", "Alice");
            fake.WriteFound(device);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "DeviceFound event not received within 2 s.");
            await consumerTask;

            // Assert
            var foundEvent = received.OfType<DiscovererEvent.DeviceFound>().FirstOrDefault();
            Assert.IsNotNull(foundEvent);
            Assert.AreEqual(device.Id, foundEvent.Device.Id);

            await discoverer.StopAsync();
        }

        [TestMethod]
        public async Task DeviceLost_Event_WhenDeviceLost()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.DeviceLost>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            var device = new NearbyDevice("peer-1", "Alice");
            fake.WriteFound(device);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceFound));

            // Act
            fake.WriteLost(device);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "DeviceLost event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is DiscovererEvent.DeviceLost));

            await discoverer.StopAsync();
        }

        [TestMethod]
        public async Task DeviceConnected_Event_WhenConnectCalled()
        {
            // Arrange
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.DeviceConnected>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            fake.WriteFound(device);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceFound));

            // Act
            await discoverer.ConnectAsync(device);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "DeviceConnected event not received within 2 s.");
            await consumerTask;

            // Assert
            var connectedEvent = received.OfType<DiscovererEvent.DeviceConnected>().FirstOrDefault();
            Assert.IsNotNull(connectedEvent);
            Assert.AreSame(conn, connectedEvent.Connection);

            await discoverer.StopAsync();
            conn.CompleteReceive();
        }

        [TestMethod]
        public async Task DeviceDisconnected_Event_WhenConnectionDrops()
        {
            // Arrange
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.DeviceDisconnected>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            fake.WriteFound(device);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceFound));
            await discoverer.ConnectAsync(device);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceConnected));

            // Act — trigger disconnect
            conn.CompleteReceive();

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "DeviceDisconnected event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is DiscovererEvent.DeviceDisconnected));

            await discoverer.StopAsync();
        }

        [TestMethod]
        public async Task Synchronized_Replays_VisibleDevices()
        {
            // Arrange — write a Found event BEFORE starting EventsAsync
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            var device = new NearbyDevice("peer-1", "Alice");
            fake.WriteFound(device);

            // Give the run loop time to process the Found event into the snapshot
            await Task.Delay(100);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            // Start EventsAsync AFTER the device was processed
            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (ev is DiscovererEvent.Synchronized)
                    {
                        break;
                    }
                }
            }, cts.Token);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "Synchronized sentinel not received within 2 s.");
            await consumerTask;

            // Assert — DeviceFound should appear BEFORE Synchronized
            var syncIndex = received.FindIndex(e => e is DiscovererEvent.Synchronized);
            var foundIndex = received.FindIndex(e => e is DiscovererEvent.DeviceFound);
            Assert.IsGreaterThanOrEqualTo(foundIndex, 0, "DeviceFound replay event not found.");
            Assert.IsLessThan(syncIndex, foundIndex, "DeviceFound should appear before Synchronized.");

            await discoverer.StopAsync();
        }

        [TestMethod]
        public async Task Synchronized_Replays_ActiveConnections()
        {
            // Arrange — connect a device BEFORE starting EventsAsync
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);

            fake.WriteFound(device);
            await Task.Delay(100);

            await discoverer.ConnectAsync(device);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            // Start EventsAsync AFTER the connection was established
            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (ev is DiscovererEvent.Synchronized)
                    {
                        break;
                    }
                }
            }, cts.Token);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "Synchronized sentinel not received within 2 s.");
            await consumerTask;

            // Assert — DeviceConnected should appear BEFORE Synchronized
            var syncIndex = received.FindIndex(e => e is DiscovererEvent.Synchronized);
            var connectedIndex = received.FindIndex(e => e is DiscovererEvent.DeviceConnected);
            Assert.IsGreaterThanOrEqualTo(connectedIndex, 0, "DeviceConnected replay event not found.");
            Assert.IsLessThan(syncIndex, connectedIndex, "DeviceConnected should appear before Synchronized.");

            await discoverer.StopAsync();
            conn.CompleteReceive();
        }
    }

    // ===========================================================================
    // ConnectAsync
    // ===========================================================================
    [TestClass]
    public sealed class ConnectAsync
    {
        [TestMethod]
        public async Task ConnectAsync_EmitsDeviceConnectedEvent()
        {
            // Arrange
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.DeviceConnected>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            fake.WriteFound(device);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceFound));

            // Act
            var result = await discoverer.ConnectAsync(device);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "DeviceConnected event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is DiscovererEvent.DeviceConnected));
            Assert.AreSame(conn, result);

            await discoverer.StopAsync();
            conn.CompleteReceive();
        }
    }

    // ===========================================================================
    // MonitorConnection
    // ===========================================================================
    [TestClass]
    public sealed class MonitorConnection
    {
        [TestMethod]
        public async Task MonitorConnection_EmitsDeviceDisconnectedWhenDisconnected()
        {
            // Arrange
            var device = new NearbyDevice("peer-1", "Alice");
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            var (conn, _) = CreateConnection(device);
            fake.ConnectTcs.SetResult(conn);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.DeviceDisconnected>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            fake.WriteFound(device);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceFound));
            await discoverer.ConnectAsync(device);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceConnected));

            // Act — trigger disconnect
            conn.CompleteReceive();

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "DeviceDisconnected event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is DiscovererEvent.DeviceDisconnected));

            await discoverer.StopAsync();
        }
    }

    // ===========================================================================
    // PayloadForwarding — payloads from connected peers arrive on EventsAsync
    // ===========================================================================
    [TestClass]
    public sealed class PayloadForwarding
    {
        [TestMethod]
        public async Task PayloadFromConnectedPeer_ArrivesOnUnifiedStream()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var (discoverer, conn, receiveChannel) = await SetupConnectedDiscoverer(fake);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.PayloadReceived>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            // Act
            var payload = new BytesPayload([1, 2, 3]);
            receiveChannel.Writer.TryWrite(payload);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "PayloadReceived event not received within 2 s.");
            await consumerTask;

            // Assert
            var payloadEvent = received.OfType<DiscovererEvent.PayloadReceived>().FirstOrDefault();
            Assert.IsNotNull(payloadEvent);
            Assert.AreSame(payload, payloadEvent.Payload);
            Assert.AreSame(conn, payloadEvent.Connection);

            await discoverer.StopAsync();
            conn.CompleteReceive();
        }

        [TestMethod]
        public async Task PayloadsFromMultipleConnections_AllArrive()
        {
            // Arrange — two connections via MultiConnectFake
            var device1 = new NearbyDevice("peer-1", "Alice");
            var device2 = new NearbyDevice("peer-2", "Bob");

            var (conn1, ch1) = CreateConnection(device1);
            var (conn2, ch2) = CreateConnection(device2);

            var fake = new MultiConnectFake(conn1, conn2);
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.PayloadReceived>().Skip(1).Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            fake.WriteFound(device1);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceFound));
            await discoverer.ConnectAsync(device1);
            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.DeviceConnected));

            fake.WriteFound(device2);
            await WaitForAsync(() => received.OfType<DiscovererEvent.DeviceFound>().Skip(1).Any());
            await discoverer.ConnectAsync(device2);
            await WaitForAsync(() => received.OfType<DiscovererEvent.DeviceConnected>().Skip(1).Any());

            // Act
            var payload1 = new BytesPayload([10]);
            var payload2 = new BytesPayload([20]);
            ch1.Writer.TryWrite(payload1);
            ch2.Writer.TryWrite(payload2);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "PayloadReceived events not received within 2 s.");
            await consumerTask;

            // Assert
            var payloadEvents = received.OfType<DiscovererEvent.PayloadReceived>().ToList();
            Assert.HasCount(2, payloadEvents);
            Assert.IsTrue(payloadEvents.Any(e => ReferenceEquals(e.Payload, payload1)));
            Assert.IsTrue(payloadEvents.Any(e => ReferenceEquals(e.Payload, payload2)));

            await discoverer.StopAsync();
            conn1.CompleteReceive();
            conn2.CompleteReceive();
        }

        [TestMethod]
        public async Task ExitsCleanlyCancelTokenCanceled()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var discoverer = new NearbyDiscoverer(fake);
            await discoverer.StartAsync();

            using var cts = new CancellationTokenSource();

            var enumerateTask = Task.Run(async () =>
            {
                await foreach (var _ in discoverer.EventsAsync(cts.Token))
                {
                    // drain — consume until cancelled
                }
            });

            // Act
            await cts.CancelAsync();

            // Assert — must exit within timeout
            var completedTask = await Task.WhenAny(enumerateTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(enumerateTask, completedTask, "EventsAsync did not exit within 2 s after cancellation.");

            if (enumerateTask.IsFaulted)
            {
                Assert.IsInstanceOfType<OperationCanceledException>(enumerateTask.Exception!.InnerException);
            }

            await discoverer.StopAsync();
        }

        [TestMethod]
        public async Task PayloadWrittenBeforeDisconnect_IsNotLost()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var (discoverer, conn, receiveChannel) = await SetupConnectedDiscoverer(fake);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<DiscovererEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<DiscovererEvent.PayloadReceived>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is DiscovererEvent.Synchronized));

            // Act — write payload then disconnect; payload must not be lost on disconnect
            var payload = new BytesPayload([99]);
            receiveChannel.Writer.TryWrite(payload);
            conn.CompleteReceive();

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "PayloadReceived event not received within 2 s.");
            await consumerTask;

            // Assert
            var payloadEvent = received.OfType<DiscovererEvent.PayloadReceived>().FirstOrDefault();
            Assert.IsNotNull(payloadEvent);
            Assert.AreSame(payload, payloadEvent.Payload);

            await discoverer.StopAsync();
        }
    }

    // ===========================================================================
    // EventStreamBehavior — channel lifecycle, error propagation, and backpressure
    // ===========================================================================
    [TestClass]
    public sealed class EventStreamBehavior
    {
        // Verifies that _eventChannel.Writer.TryComplete() in StopAsync unblocks a
        // consumer that is awaiting EventsAsync with no cancellation token.
        [TestMethod]
        public async Task StopAsync_WithoutCancellationToken_CompletesEventStream()
        {
            // Arrange
            var discoverer = new NearbyDiscoverer(CreateSubstitute());
            await discoverer.StartAsync();

            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var streamTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync()) // intentionally no CancellationToken
                {
                    if (ev is DiscovererEvent.Synchronized)
                    {
                        synchronizedTcs.TrySetResult();
                    }
                }
            });

            await synchronizedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act
            await discoverer.StopAsync();

            // Assert
            await streamTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(streamTask.IsCompletedSuccessfully);
        }

        // StartAsync completes the old _eventChannel and creates a new one.
        // A second EventsAsync call after a Stop/Start cycle must read from the
        // new channel and still emit Synchronized.
        [TestMethod]
        public async Task StartAsync_AfterStop_ProducesFreshStream()
        {
            // Arrange
            var discoverer = new NearbyDiscoverer(CreateSubstitute());

            // First lifecycle — start, consume Synchronized, stop
            await discoverer.StartAsync();
            var firstSyncTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstStreamTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync())
                {
                    if (ev is DiscovererEvent.Synchronized) { firstSyncTcs.TrySetResult(); break; }
                }
            });
            await firstSyncTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await discoverer.StopAsync();
            await firstStreamTask.WaitAsync(TimeSpan.FromSeconds(2));

            // Act — second lifecycle on the same instance
            await discoverer.StartAsync();
            var secondSyncTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStreamTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync())
                {
                    if (ev is DiscovererEvent.Synchronized) { secondSyncTcs.TrySetResult(); break; }
                }
            });

            // Assert — recreated channel emits Synchronized; the completed channel was not reused
            await secondSyncTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(secondSyncTcs.Task.IsCompletedSuccessfully);
            await discoverer.StopAsync();
            await secondStreamTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        // When the platform completes INearbyConnections.DiscoverAsync with an error,
        // RunLoopAsync forwards it via _eventChannel.Writer.TryComplete(ex) so the
        // consumer's await foreach throws NearbyDiscoveryException.
        [TestMethod]
        public async Task DiscoverAsync_PlatformError_PropagatesExceptionToEventStream()
        {
            // Arrange
            var discoverChannel = Channel.CreateUnbounded<NearbyDeviceEvent>();
            var discoverer = new NearbyDiscoverer(CreateSubstitute(discoverChannel));
            await discoverer.StartAsync();

            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Exception? caughtException = null;

            var streamTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var ev in discoverer.EventsAsync())
                    {
                        if (ev is DiscovererEvent.Synchronized)
                        {
                            synchronizedTcs.TrySetResult();
                        }
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            await synchronizedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act — platform signals it could not start browsing
            var platformError = new NearbyDiscoveryException("Platform refused to start browsing.");
            discoverChannel.Writer.TryComplete(platformError);

            // Assert — consumer receives the platform exception through the event stream
            await streamTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsInstanceOfType<NearbyDiscoveryException>(caughtException);
        }

        // _eventChannel is created at field initialisation time, so EventsAsync is safe
        // to call before StartAsync. The snapshot is empty and the channel is open but
        // unwritten-to, so Synchronized arrives immediately and the stream then blocks
        // until StopAsync (or a CancellationToken) terminates it.
        [TestMethod]
        public async Task EventsAsync_BeforeStartAsync_YieldsSynchronizedThenBlocks()
        {
            // Arrange
            var discoverer = new NearbyDiscoverer(CreateSubstitute());
            // StartAsync is intentionally not called

            using var cts = new CancellationTokenSource();
            var received = new List<DiscovererEvent>();
            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Act
            var streamTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (ev is DiscovererEvent.Synchronized)
                    {
                        synchronizedTcs.TrySetResult();
                    }
                }
            });

            // Assert — Synchronized is emitted immediately via the snapshot yield path
            await synchronizedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsInstanceOfType<DiscovererEvent.Synchronized>(received[0]);

            // Assert — stream blocks after Synchronized; no items arrive without StartAsync
            await Task.Delay(100);
            Assert.HasCount(1, received, "No further events expected before StartAsync.");

            // Cleanup — cancel to unblock the hanging ReadAllAsync
            await cts.CancelAsync();
            await Task.WhenAny(streamTask, Task.Delay(TimeSpan.FromSeconds(1)));
        }

        // Platform callbacks write to an unbounded channel without waiting for the
        // consumer. A consumer that processes each event slowly must still receive every
        // event without any being dropped.
        [TestMethod]
        public async Task EventsAsync_SlowConsumer_DoesNotLoseEvents()
        {
            // Arrange
            const int EventCount = 20;
            var discoverChannel = Channel.CreateUnbounded<NearbyDeviceEvent>();
            var discoverer = new NearbyDiscoverer(CreateSubstitute(discoverChannel));
            await discoverer.StartAsync();

            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var received = new List<DiscovererEvent>();
            var allArrivedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var streamTask = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync())
                {
                    if (ev is DiscovererEvent.Synchronized) { synchronizedTcs.TrySetResult(); continue; }
                    received.Add(ev);
                    await Task.Delay(10); // simulate slow consumer
                    if (received.Count == EventCount)
                    {
                        allArrivedTcs.TrySetResult();
                    }
                }
            });

            await synchronizedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act — rapidly produce device-found events while the consumer processes them slowly
            for (var i = 0; i < EventCount; i++)
            {
                discoverChannel.Writer.TryWrite(Found($"peer-{i}", $"Device {i}"));
            }

            // Assert — unbounded buffering means no event is dropped despite slow consumption
            await allArrivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.HasCount(EventCount, received);

            await discoverer.StopAsync();
            await streamTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        // _eventChannel uses SingleReader = false, allowing concurrent readers, but each
        // item is dequeued by exactly one reader. Consumers who call EventsAsync twice
        // concurrently will each see a disjoint subset of events, not full copies.
        // The Synchronized sentinel is yielded by the iterator itself so both consumers
        // receive their own copy of it; only items from _eventChannel are split.
        [TestMethod]
        public async Task EventsAsync_TwoConcurrentConsumers_SplitEventsNotBroadcast()
        {
            // Arrange
            const int EventCount = 10;
            var discoverChannel = Channel.CreateUnbounded<NearbyDeviceEvent>();
            var discoverer = new NearbyDiscoverer(CreateSubstitute(discoverChannel));
            await discoverer.StartAsync();

            var sync1Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sync2Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumer1Events = new List<DiscovererEvent>();
            var consumer2Events = new List<DiscovererEvent>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var consumer1 = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    if (ev is DiscovererEvent.Synchronized) { sync1Tcs.TrySetResult(); continue; }
                    consumer1Events.Add(ev);
                }
            });

            var consumer2 = Task.Run(async () =>
            {
                await foreach (var ev in discoverer.EventsAsync(cts.Token))
                {
                    if (ev is DiscovererEvent.Synchronized) { sync2Tcs.TrySetResult(); continue; }
                    consumer2Events.Add(ev);
                }
            });

            // Ensure both consumers are past Synchronized before producing events
            await Task.WhenAll(sync1Tcs.Task, sync2Tcs.Task).WaitAsync(TimeSpan.FromSeconds(2));

            // Act — write events while both consumers are actively reading
            for (var i = 0; i < EventCount; i++)
            {
                discoverChannel.Writer.TryWrite(Found($"peer-{i}", $"Device {i}"));
            }

            await Task.Delay(300); // allow both consumers to drain their share
            await discoverer.StopAsync();
            await Task.WhenAll(consumer1, consumer2).WaitAsync(TimeSpan.FromSeconds(2));

            // Assert — every event is consumed exactly once across both consumers
            var total = consumer1Events.Count + consumer2Events.Count;
            Assert.AreEqual(EventCount, total,
                "Channel items are consumed once; concurrent consumers split events, not broadcast them.");
        }
    }
}
