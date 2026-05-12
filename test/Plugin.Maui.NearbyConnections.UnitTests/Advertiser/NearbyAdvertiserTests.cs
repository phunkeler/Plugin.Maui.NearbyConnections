using System.Threading.Channels;
using NSubstitute;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Advertiser")]
public sealed class NearbyAdvertiserTests
{
    // ---------------------------------------------------------------------------
    // FakeNearbyConnections — backed by live channels that test methods write to.
    // ---------------------------------------------------------------------------
    private sealed class FakeNearbyConnections : INearbyConnections
    {
        readonly Channel<NearbyConnectionRequest> _advertiseChannel =
            Channel.CreateUnbounded<NearbyConnectionRequest>();

        readonly Channel<NearbyDeviceEvent> _discoverChannel =
            Channel.CreateUnbounded<NearbyDeviceEvent>();

        public TaskCompletionSource<NearbyConnection> ConnectTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void WriteRequest(NearbyConnectionRequest request)
            => _advertiseChannel.Writer.TryWrite(request);

        public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var req in _advertiseChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return req;
            }
        }

        public IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(CancellationToken cancellationToken = default)
            => _discoverChannel.Reader.ReadAllAsync(cancellationToken);

        public Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
            => ConnectTcs.Task.WaitAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // ---------------------------------------------------------------------------
    // Helper: NSubstitute fake wired to an in-memory advertise channel.
    // Used by EventStreamBehavior tests that need to control the channel directly.
    // ---------------------------------------------------------------------------
    static INearbyConnections CreateSubstitute(Channel<NearbyConnectionRequest>? advertiseChannel = null)
    {
        var ch = advertiseChannel ?? Channel.CreateUnbounded<NearbyConnectionRequest>();
        var inner = Substitute.For<INearbyConnections>();
        inner.AdvertiseAsync(Arg.Any<CancellationToken>())
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
    // Helper: create a NearbyConnectionRequest that resolves to conn on accept.
    // ---------------------------------------------------------------------------
    static NearbyConnectionRequest CreateRequest(NearbyConnection conn, NearbyDevice? device = null)
        => new(
            device ?? conn.RemoteDevice,
            acceptFactory: _ => Task.FromResult(conn),
            rejectFactory: _ => Task.CompletedTask);

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

    // ===========================================================================
    // StartAsync
    // ===========================================================================
    [TestClass]
    public sealed class StartAsync
    {
        [TestMethod]
        public async Task StartAsync_SetsIsAdvertising_True()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);

            // Act
            await advertiser.StartAsync();

            // Assert
            Assert.IsTrue(advertiser.IsAdvertising);

            await advertiser.StopAsync();
        }
    }

    // ===========================================================================
    // StopAsync
    // ===========================================================================
    [TestClass]
    public sealed class StopAsync
    {
        [TestMethod]
        public async Task StopAsync_SetsIsAdvertising_False()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();
            Assert.IsTrue(advertiser.IsAdvertising);

            // Act
            await advertiser.StopAsync();

            // The RunLoopAsync finally block sets IsAdvertising = false on a background task;
            // allow a short window for it to complete.
            await WaitForAsync(() => !advertiser.IsAdvertising);

            // Assert
            Assert.IsFalse(advertiser.IsAdvertising);
        }

        [TestMethod]
        public async Task StartAsync_WhenCalledTwice_CancelsPreviousLoop()
        {
            // Arrange — first loop; use a slow channel so it stays open
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();
            Assert.IsTrue(advertiser.IsAdvertising);

            // Act — second StartAsync cancels the first and starts a new loop
            await advertiser.StartAsync();

            // Assert
            Assert.IsTrue(advertiser.IsAdvertising);

            await advertiser.StopAsync();
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
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            AdvertiserEvent? firstEvent = null;

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    firstEvent = ev;
                    break;
                }
            }, cts.Token);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "EventsAsync did not emit Synchronized within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsInstanceOfType<AdvertiserEvent.Synchronized>(firstEvent);

            await advertiser.StopAsync();
        }

        [TestMethod]
        public async Task ConnectionRequested_Event_WhenRequestArrives()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    // Collect Synchronized + one ConnectionRequested
                    if (received.Count >= 2)
                    {
                        break;
                    }
                }
            }, cts.Token);

            // Wait for the stream to emit Synchronized (consumer started)
            await WaitForAsync(() => received.Count >= 1);

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "ConnectionRequested event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is AdvertiserEvent.ConnectionRequested));

            await advertiser.StopAsync();
        }

        [TestMethod]
        public async Task ConnectionAccepted_Event_WhenAcceptCalled()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<AdvertiserEvent.ConnectionAccepted>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            // Wait for Synchronized
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            // Wait for ConnectionRequested
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));

            // Act
            await advertiser.AcceptAsync(request);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "ConnectionAccepted event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            await advertiser.StopAsync();
            conn.CompleteReceive();
        }

        [TestMethod]
        public async Task ConnectionDropped_Event_WhenConnectionDisconnects()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<AdvertiserEvent.ConnectionDropped>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            // Wait for Synchronized
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));
            await advertiser.AcceptAsync(request);
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            // Act — trigger disconnect
            conn.CompleteReceive();

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "ConnectionDropped event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is AdvertiserEvent.ConnectionDropped));

            await advertiser.StopAsync();
        }

        [TestMethod]
        public async Task RejectAsync_DoesNotEmitConnectionAcceptedEvent()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    // Collect until cancelled
                }
            }, cts.Token);

            // Wait for Synchronized
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));

            // Act
            await advertiser.RejectAsync(request);

            // Allow time for any spurious events
            await Task.Delay(100);

            // Cancel the consumer
            await cts.CancelAsync();
            await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(1)));

            // Assert — Synchronized and ConnectionRequested should be present, but no ConnectionAccepted
            Assert.IsTrue(received.Any(e => e is AdvertiserEvent.Synchronized));
            Assert.IsTrue(received.Any(e => e is AdvertiserEvent.ConnectionRequested));
            Assert.IsFalse(received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            await advertiser.StopAsync();
        }

        [TestMethod]
        public async Task Synchronized_Replays_PendingRequests()
        {
            // Arrange — write request BEFORE starting EventsAsync
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            // Give the run loop time to process the request into the snapshot
            await Task.Delay(100);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            // Start EventsAsync AFTER the request was processed
            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (ev is AdvertiserEvent.Synchronized)
                    {
                        break;
                    }
                }
            }, cts.Token);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "Synchronized sentinel not received within 2 s.");
            await consumerTask;

            // Assert — ConnectionRequested should appear BEFORE Synchronized
            var syncIndex = received.FindIndex(e => e is AdvertiserEvent.Synchronized);
            var reqIndex = received.FindIndex(e => e is AdvertiserEvent.ConnectionRequested);
            Assert.IsGreaterThanOrEqualTo(reqIndex, 0, "ConnectionRequested replay event not found.");
            Assert.IsLessThan(syncIndex, reqIndex, "ConnectionRequested should appear before Synchronized.");

            await advertiser.StopAsync();
        }

        [TestMethod]
        public async Task Synchronized_Replays_ActiveConnections()
        {
            // Arrange — accept a connection BEFORE starting EventsAsync
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            // Wait for the run loop to process the request
            await Task.Delay(100);

            await advertiser.AcceptAsync(request);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            // Start EventsAsync AFTER the connection was accepted
            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (ev is AdvertiserEvent.Synchronized)
                    {
                        break;
                    }
                }
            }, cts.Token);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "Synchronized sentinel not received within 2 s.");
            await consumerTask;

            // Assert — ConnectionAccepted should appear BEFORE Synchronized
            var syncIndex = received.FindIndex(e => e is AdvertiserEvent.Synchronized);
            var acceptedIndex = received.FindIndex(e => e is AdvertiserEvent.ConnectionAccepted);
            Assert.IsGreaterThanOrEqualTo(acceptedIndex, 0, "ConnectionAccepted replay event not found.");
            Assert.IsLessThan(syncIndex, acceptedIndex, "ConnectionAccepted should appear before Synchronized.");

            await advertiser.StopAsync();
            conn.CompleteReceive();
        }
    }

    // ===========================================================================
    // AcceptAsync
    // ===========================================================================
    [TestClass]
    public sealed class AcceptAsync
    {
        [TestMethod]
        public async Task AcceptAsync_EmitsConnectionAcceptedEvent()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<AdvertiserEvent.ConnectionAccepted>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));

            // Act
            await advertiser.AcceptAsync(request);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "ConnectionAccepted event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            await advertiser.StopAsync();
            conn.CompleteReceive();
        }

        [TestMethod]
        public async Task RejectAsync_NoConnectionAcceptedEvent()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));

            // Act
            await advertiser.RejectAsync(request);

            // Allow a short window for any spurious events
            await Task.Delay(100);
            await cts.CancelAsync();
            await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(1)));

            // Assert
            Assert.IsFalse(received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            await advertiser.StopAsync();
        }
    }

    // ===========================================================================
    // MonitorConnection
    // ===========================================================================
    [TestClass]
    public sealed class MonitorConnection
    {
        [TestMethod]
        public async Task MonitorConnection_EmitsConnectionDroppedWhenDisconnected()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<AdvertiserEvent.ConnectionDropped>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, _) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));
            await advertiser.AcceptAsync(request);
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            // Act — trigger disconnect by completing the connection
            conn.CompleteReceive();

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "ConnectionDropped event not received within 2 s.");
            await consumerTask;

            // Assert
            Assert.IsTrue(received.Any(e => e is AdvertiserEvent.ConnectionDropped));

            await advertiser.StopAsync();
        }
    }

    // ===========================================================================
    // PayloadForwarding — payloads from accepted connections arrive on EventsAsync
    // ===========================================================================
    [TestClass]
    public sealed class PayloadForwarding
    {
        [TestMethod]
        public async Task PayloadFromAcceptedConnection_ArrivesOnUnifiedStream()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<AdvertiserEvent.PayloadReceived>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            // Wait for Synchronized before triggering events
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, receiveChannel) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));
            await advertiser.AcceptAsync(request);
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            // Act — write payload; ForwardPayloadsAsync will pick it up
            var payload = new BytesPayload([1, 2, 3]);
            receiveChannel.Writer.TryWrite(payload);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "PayloadReceived event not received within 2 s.");
            await consumerTask;

            // Assert
            var payloadEvent = received.OfType<AdvertiserEvent.PayloadReceived>().FirstOrDefault();
            Assert.IsNotNull(payloadEvent);
            Assert.AreSame(payload, payloadEvent.Payload);
            Assert.AreSame(conn, payloadEvent.Connection);

            await advertiser.StopAsync();
            conn.CompleteReceive();
        }

        [TestMethod]
        public async Task PayloadsFromMultipleConnections_AllArrive()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<AdvertiserEvent.PayloadReceived>().Skip(1).Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn1, ch1) = CreateConnection(new NearbyDevice("peer-1", "Alice"));
            var (conn2, ch2) = CreateConnection(new NearbyDevice("peer-2", "Bob"));

            var request1 = CreateRequest(conn1);
            var request2 = CreateRequest(conn2);

            fake.WriteRequest(request1);
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));
            await advertiser.AcceptAsync(request1);
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            fake.WriteRequest(request2);
            await WaitForAsync(() => received.OfType<AdvertiserEvent.ConnectionRequested>().Skip(1).Any());
            await advertiser.AcceptAsync(request2);
            await WaitForAsync(() => received.OfType<AdvertiserEvent.ConnectionAccepted>().Skip(1).Any());

            // Act
            var payload1 = new BytesPayload([10]);
            var payload2 = new BytesPayload([20]);
            ch1.Writer.TryWrite(payload1);
            ch2.Writer.TryWrite(payload2);

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "PayloadReceived events not received within 2 s.");
            await consumerTask;

            // Assert
            var payloadEvents = received.OfType<AdvertiserEvent.PayloadReceived>().ToList();
            Assert.HasCount(2, payloadEvents);
            Assert.IsTrue(payloadEvents.Any(e => ReferenceEquals(e.Payload, payload1)));
            Assert.IsTrue(payloadEvents.Any(e => ReferenceEquals(e.Payload, payload2)));

            await advertiser.StopAsync();
            conn1.CompleteReceive();
            conn2.CompleteReceive();
        }

        [TestMethod]
        public async Task ExitsCleanlyCancelTokenCanceled()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource();

            var enumerateTask = Task.Run(async () =>
            {
                await foreach (var _ in advertiser.EventsAsync(cts.Token))
                {
                    // drain — consume until cancelled
                }
            });

            // Act
            await cts.CancelAsync();

            // Assert — enumerateTask completes (does not hang); OperationCanceledException is expected
            var completedTask = await Task.WhenAny(enumerateTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(enumerateTask, completedTask, "EventsAsync did not exit within 2 s after cancellation.");

            // The task should be canceled or faulted with OperationCanceledException — either is acceptable.
            if (enumerateTask.IsFaulted)
            {
                Assert.IsInstanceOfType<OperationCanceledException>(enumerateTask.Exception!.InnerException);
            }

            await advertiser.StopAsync();
        }

        [TestMethod]
        public async Task PayloadWrittenBeforeDisconnect_IsNotLost()
        {
            // Arrange
            var fake = new FakeNearbyConnections();
            var advertiser = new NearbyAdvertiser(fake);
            await advertiser.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = new List<AdvertiserEvent>();

            var consumerTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);
                    if (received.OfType<AdvertiserEvent.PayloadReceived>().Any())
                    {
                        break;
                    }
                }
            }, cts.Token);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.Synchronized));

            var (conn, receiveChannel) = CreateConnection();
            var request = CreateRequest(conn);
            fake.WriteRequest(request);

            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionRequested));
            await advertiser.AcceptAsync(request);
            await WaitForAsync(() => received.Any(e => e is AdvertiserEvent.ConnectionAccepted));

            // Act — write payload then disconnect; payload must not be lost on disconnect
            var payload = new BytesPayload([99]);
            receiveChannel.Writer.TryWrite(payload);
            conn.CompleteReceive();

            var completed = await Task.WhenAny(consumerTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(consumerTask, completed, "PayloadReceived event not received within 2 s.");
            await consumerTask;

            // Assert
            var payloadEvent = received.OfType<AdvertiserEvent.PayloadReceived>().FirstOrDefault();
            Assert.IsNotNull(payloadEvent);
            Assert.AreSame(payload, payloadEvent.Payload);

            await advertiser.StopAsync();
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
            var advertiser = new NearbyAdvertiser(CreateSubstitute());
            await advertiser.StartAsync();

            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var streamTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync()) // intentionally no CancellationToken
                {
                    if (ev is AdvertiserEvent.Synchronized)
                    {
                        synchronizedTcs.TrySetResult();
                    }
                }
            });

            await synchronizedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act
            await advertiser.StopAsync();

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
            var advertiser = new NearbyAdvertiser(CreateSubstitute());

            // First lifecycle — start, consume Synchronized, stop
            await advertiser.StartAsync();
            var firstSyncTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstStreamTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync())
                {
                    if (ev is AdvertiserEvent.Synchronized)
                    {
                        firstSyncTcs.TrySetResult();
                        break;
                    }
                }
            });
            await firstSyncTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await advertiser.StopAsync();
            await firstStreamTask.WaitAsync(TimeSpan.FromSeconds(2));

            // Act — second lifecycle on the same instance
            await advertiser.StartAsync();
            var secondSyncTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStreamTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync())
                {
                    if (ev is AdvertiserEvent.Synchronized)
                    {
                        secondSyncTcs.TrySetResult();
                        break;
                    }
                }
            });

            // Assert — recreated channel emits Synchronized; the completed channel was not reused
            await secondSyncTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(secondSyncTcs.Task.IsCompletedSuccessfully);
            await advertiser.StopAsync();
            await secondStreamTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        // When the platform completes INearbyConnections.AdvertiseAsync with an error,
        // RunLoopAsync forwards it via _eventChannel.Writer.TryComplete(ex) so the
        // consumer's await foreach throws NearbyAdvertisingException.
        [TestMethod]
        public async Task AdvertiseAsync_PlatformError_PropagatesExceptionToEventStream()
        {
            // Arrange
            var advertiseChannel = Channel.CreateUnbounded<NearbyConnectionRequest>();
            var advertiser = new NearbyAdvertiser(CreateSubstitute(advertiseChannel));
            await advertiser.StartAsync();

            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Exception? caughtException = null;

            var streamTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var ev in advertiser.EventsAsync())
                    {
                        if (ev is AdvertiserEvent.Synchronized)
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

            // Act — platform signals it could not start advertising
            var platformError = new NearbyAdvertisingException("Platform refused to start advertising.");
            advertiseChannel.Writer.TryComplete(platformError);

            // Assert — consumer receives the platform exception through the event stream
            await streamTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsInstanceOfType<NearbyAdvertisingException>(caughtException);
        }

        // _eventChannel is created at field initialisation time, so EventsAsync is safe
        // to call before StartAsync. The snapshot is empty and the channel is open but
        // unwritten-to, so Synchronized arrives immediately and the stream then blocks
        // until StopAsync (or a CancellationToken) terminates it.
        [TestMethod]
        public async Task EventsAsync_BeforeStartAsync_YieldsSynchronizedThenBlocks()
        {
            // Arrange
            var advertiser = new NearbyAdvertiser(CreateSubstitute());
            // StartAsync is intentionally not called

            using var cts = new CancellationTokenSource();
            var received = new List<AdvertiserEvent>();
            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Act
            var streamTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    received.Add(ev);

                    if (ev is AdvertiserEvent.Synchronized)
                    {
                        synchronizedTcs.TrySetResult();
                    }
                }
            });

            // Assert — Synchronized is emitted immediately via the snapshot yield path
            await synchronizedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsInstanceOfType<AdvertiserEvent.Synchronized>(received[0]);

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
            const int RequestCount = 20;
            var advertiseChannel = Channel.CreateUnbounded<NearbyConnectionRequest>();
            var advertiser = new NearbyAdvertiser(CreateSubstitute(advertiseChannel));
            await advertiser.StartAsync();

            var synchronizedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var received = new List<AdvertiserEvent>();
            var allArrivedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var streamTask = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync())
                {
                    if (ev is AdvertiserEvent.Synchronized)
                    {
                        synchronizedTcs.TrySetResult();
                        continue;
                    }

                    received.Add(ev);

                    await Task.Delay(10); // simulate slow consumer

                    if (received.Count == RequestCount)
                    {
                        allArrivedTcs.TrySetResult();
                    }
                }
            });

            await synchronizedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act — rapidly produce all requests while the consumer processes them slowly
            for (var i = 0; i < RequestCount; i++)
            {
                var (conn, _) = CreateConnection(new NearbyDevice($"peer-{i}", $"Device {i}"));
                advertiseChannel.Writer.TryWrite(new NearbyConnectionRequest(
                    conn.RemoteDevice,
                    acceptFactory: _ => Task.FromResult(conn),
                    rejectFactory: _ => Task.CompletedTask));
            }

            // Assert — unbounded buffering means no event is dropped despite slow consumption
            await allArrivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.HasCount(RequestCount, received);

            await advertiser.StopAsync();
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
            const int RequestCount = 10;
            var advertiseChannel = Channel.CreateUnbounded<NearbyConnectionRequest>();
            var advertiser = new NearbyAdvertiser(CreateSubstitute(advertiseChannel));
            await advertiser.StartAsync();

            var sync1Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sync2Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumer1Events = new List<AdvertiserEvent>();
            var consumer2Events = new List<AdvertiserEvent>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var consumer1 = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    if (ev is AdvertiserEvent.Synchronized) { sync1Tcs.TrySetResult(); continue; }
                    consumer1Events.Add(ev);
                }
            });

            var consumer2 = Task.Run(async () =>
            {
                await foreach (var ev in advertiser.EventsAsync(cts.Token))
                {
                    if (ev is AdvertiserEvent.Synchronized) { sync2Tcs.TrySetResult(); continue; }
                    consumer2Events.Add(ev);
                }
            });

            // Ensure both consumers are past Synchronized before producing events
            await Task.WhenAll(sync1Tcs.Task, sync2Tcs.Task).WaitAsync(TimeSpan.FromSeconds(2));

            // Act — write events while both consumers are actively reading
            for (var i = 0; i < RequestCount; i++)
            {
                var (conn, _) = CreateConnection(new NearbyDevice($"peer-{i}", $"Device {i}"));
                advertiseChannel.Writer.TryWrite(new NearbyConnectionRequest(
                    conn.RemoteDevice,
                    acceptFactory: _ => Task.FromResult(conn),
                    rejectFactory: _ => Task.CompletedTask));
            }

            await Task.Delay(300); // allow both consumers to drain their share
            await advertiser.StopAsync();
            await Task.WhenAll(consumer1, consumer2).WaitAsync(TimeSpan.FromSeconds(2));

            // Assert — every event is consumed exactly once across both consumers
            var total = consumer1Events.Count + consumer2Events.Count;
            Assert.AreEqual(RequestCount, total,
                "Channel items are consumed once; concurrent consumers split events, not broadcast them.");
        }
    }
}
