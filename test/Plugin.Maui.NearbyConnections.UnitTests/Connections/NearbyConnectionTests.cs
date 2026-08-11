using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class NearbyConnectionTests
{
    [TestClass]
    public sealed class RemoteDevice : NearbyConnectionTests
    {
        [TestMethod]
        public void ReturnsConstructedDevice()
        {
            // Arrange
            var device = new NearbyDevice("peer-42", "Bob");
            var connection = Create.Connection(device: device);

            // Act
            var result = connection.RemoteDevice;

            // Assert
            Assert.AreSame(device, result);
        }
    }

    [TestClass]
    public sealed class SendAsyncBytes : NearbyConnectionTests
    {
        [TestMethod]
        public async Task SendAsync_Bytes_DelegatesToSendBytes()
        {
            // Arrange
            byte[]? captured = null;
            var connection = Create.Connection(
                sendBytes: (data, _) => { captured = data; return Task.CompletedTask; });

            var payload = new byte[] { 10, 20, 30 };

            // Act
            await connection.SendAsync(payload, TestContext.CancellationToken);

            // Assert
            Assert.IsNotNull(captured);
            Assert.AreSequenceEqual(payload, captured);
        }

        [TestMethod]
        public async Task SendAsync_Bytes_ForwardsCancellationToken()
        {
            // Arrange
            CancellationToken capturedToken = default;
            var connection = Create.Connection(
                sendBytes: (_, ct) => { capturedToken = ct; return Task.CompletedTask; });

            using var cts = new CancellationTokenSource();

            // Act
            await connection.SendAsync([1], cts.Token);

            // Assert
            Assert.AreEqual(cts.Token, capturedToken);
        }

        [TestMethod]
        public async Task SendAsync_NullBytes_ThrowsArgumentNullException()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            Func<Task> act = () => connection.SendAsync((byte[])null!, TestContext.CancellationToken);

            // Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(act);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class SendAsyncFile : NearbyConnectionTests
    {
        [TestMethod]
        public async Task SendAsync_File_DelegatesToSendFile()
        {
            // Arrange
            string? capturedUri = null;
            var connection = Create.Connection(
                sendFile: (uri, _, _) => { capturedUri = uri; return Task.CompletedTask; });

            // Act
            await connection.SendAsync("/path/to/file.txt", cancellationToken: TestContext.CancellationToken);

            // Assert
            Assert.AreEqual("/path/to/file.txt", capturedUri);
        }

        [TestMethod]
        public async Task SendAsync_File_ForwardsProgress()
        {
            // Arrange
            IProgress<NearbyTransferProgress>? capturedProgress = null;
            var connection = Create.Connection(
                sendFile: (_, progress, _) => { capturedProgress = progress; return Task.CompletedTask; });

            var progress = new Progress<NearbyTransferProgress>();

            // Act
            await connection.SendAsync("/path/to/file.txt", progress, TestContext.CancellationToken);

            // Assert
            Assert.AreSame(progress, capturedProgress);
        }

        [TestMethod]
        public async Task SendAsync_NullFileUri_ThrowsArgumentNullException()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            Func<Task> act = async () => await connection.SendAsync((string)null!, cancellationToken: TestContext.CancellationToken);

            // Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(act);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class ReceiveAsync : NearbyConnectionTests
    {

        [TestMethod]
        public async Task WritePayload_YieldsPayload()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = Create.Connection(receiveChannel: receiveChannel);

            var payload = new NearbyBytesPayload([1, 2, 3]);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act — write payload then complete channel so the enumeration terminates
            receiveChannel.Writer.TryWrite(payload);
            receiveChannel.Writer.TryComplete();

            var received = new List<NearbyPayload>();
            await foreach (var item in connection.ReceiveAsync(cts.Token))
            {
                received.Add(item);
            }

            // Assert
            Assert.HasCount(1, received);
            Assert.AreSame(payload, received[0]);
        }

        [TestMethod]
        public async Task MultiplePayloads_YieldsInOrder()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = Create.Connection(receiveChannel: receiveChannel);

            var p1 = new NearbyBytesPayload([1]);
            var p2 = new NearbyBytesPayload([2]);
            var p3 = new NearbyBytesPayload([3]);

            receiveChannel.Writer.TryWrite(p1);
            receiveChannel.Writer.TryWrite(p2);
            receiveChannel.Writer.TryWrite(p3);
            receiveChannel.Writer.TryComplete();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var received = new List<NearbyPayload>();

            // Act
            await foreach (var item in connection.ReceiveAsync(cts.Token))
            {
                received.Add(item);
            }

            // Assert
            Assert.HasCount(3, received);
            Assert.AreSame(p1, received[0]);
            Assert.AreSame(p2, received[1]);
            Assert.AreSame(p3, received[2]);
        }

        [TestMethod]
        public async Task CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = Create.Connection(receiveChannel: receiveChannel);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act
            Func<Task> act = async () =>
            {
                await foreach (var _ in connection.ReceiveAsync(cts.Token))
                {
                    // drain — cancelled before first item
                }
            };

            // Assert — TaskCanceledException is a subclass of OperationCanceledException
            await Assert.ThrowsAsync<OperationCanceledException>(act);
        }

        [TestMethod]
        public async Task CalledTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            var connection = Create.Connection();
            connection.ReceiveAsync(TestContext.CancellationToken); // first call — sets guard

            // Act
            Task Act() { connection.ReceiveAsync(TestContext.CancellationToken); return Task.CompletedTask; }

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(Act);
        }

        [TestMethod]
        public async Task CalledAfterCancellation_ThrowsInvalidOperationException()
        {
            // Arrange
            var connection = Create.Connection();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            try { await foreach (var _ in connection.ReceiveAsync(cts.Token)) { } }
            catch (OperationCanceledException) { }

            // Act
            Task Act() { connection.ReceiveAsync(TestContext.CancellationToken); return Task.CompletedTask; }

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(Act);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class CompleteReceive : NearbyConnectionTests
    {
        [TestMethod]
        public async Task CompletesReceiveEnumerable()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = Create.Connection(receiveChannel: receiveChannel);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Start enumerating on a background task
            var received = new List<NearbyPayload>();
            var enumerateTask = Task.Run(async () =>
            {
                await foreach (var item in connection.ReceiveAsync(cts.Token))
                {
                    received.Add(item);
                }
            }, cts.Token);

            // Act — simulate platform disconnect completing the receive channel
            connection.CompleteReceive();

            // Assert — enumeration terminates cleanly
            await enumerateTask.WaitAsync(cts.Token);
            Assert.IsEmpty(received);
        }
    }

    [TestClass]
    public sealed class DisposeAsync : NearbyConnectionTests
    {
        [TestMethod]
        public async Task CallsDispose()
        {
            // Arrange
            var disposed = false;
            var connection = Create.Connection(
                dispose: () => { disposed = true; return ValueTask.CompletedTask; });

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(disposed);
        }

        [TestMethod]
        public async Task CompletesReceiveEnumerable()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = Create.Connection(receiveChannel: receiveChannel);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var enumerateTask = Task.Run(async () =>
            {
                await foreach (var _ in connection.ReceiveAsync(cts.Token))
                {
                    // drain — channel completes when connection is disposed
                }
            }, cts.Token);

            // Act
            await connection.DisposeAsync();

            // Assert
            await enumerateTask.WaitAsync(cts.Token);
            Assert.IsTrue(enumerateTask.IsCompletedSuccessfully);
        }

        [TestMethod]
        public async Task TryWritePayload_AfterDispose_DoesNotThrow()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = Create.Connection(receiveChannel: receiveChannel);
            await connection.DisposeAsync();

            // Act
            connection.TryWritePayload(new NearbyBytesPayload([1, 2, 3]));

            // Assert — payload silently dropped; channel writer is completed so nothing was queued
            Assert.IsFalse(receiveChannel.Reader.TryRead(out _));
        }

        // IsBeingConsumed is what lets the platform layer detect the silent-loss case: payloads
        // arriving on a connection whose ReceiveAsync was never called.
        [TestMethod]
        public void IsBeingConsumed_BeforeReceiveAsync_IsFalse()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            var consumed = connection.IsBeingConsumed;

            // Assert
            Assert.IsFalse(consumed);
        }

        // Pins the retention guarantee the whole late-consumer story rests on: the receive channel is
        // unbounded and TryWritePayload writes unconditionally, so payloads that arrive long before
        // anything calls ReceiveAsync are buffered, not dropped. A consumer that starts late drains
        // the entire backlog from connection-open, which is why the plugin needs no separate
        // payload-replay feature — only a reliable way to hand out the connection.
        [TestMethod]
        public async Task ReceiveAsync_StartedLate_DrainsPayloadsWrittenBeforeItBegan()
        {
            // Arrange
            var connection = Create.Connection();
            connection.TryWritePayload(new NearbyBytesPayload([1]));
            connection.TryWritePayload(new NearbyBytesPayload([2]));
            connection.TryWritePayload(new NearbyBytesPayload([3]));

            // Act — first call to ReceiveAsync happens only now, after all writes
            var received = new List<byte>();
            connection.CompleteReceive();

            await foreach (var payload in connection.ReceiveAsync(TestContext.CancellationToken))
            {
                received.Add(((NearbyBytesPayload)payload).Data[0]);
            }

            // Assert — every pre-subscription payload is delivered, in arrival order
            Assert.HasCount(3, received);
            Assert.AreEqual(1, received[0]);
            Assert.AreEqual(2, received[1]);
            Assert.AreEqual(3, received[2]);
        }

        [TestMethod]
        public void IsBeingConsumed_AfterReceiveAsync_IsTrue()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            _ = connection.ReceiveAsync(TestContext.CancellationToken);

            // Assert — set by calling ReceiveAsync, without needing to enumerate it
            Assert.IsTrue(connection.IsBeingConsumed);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class Disconnected : NearbyConnectionTests
    {
        [TestMethod]
        public async Task CompletesWhenCompleteReceiveCalled()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            connection.CompleteReceive();

            // Assert
            await connection.Disconnected.WaitAsync(TimeSpan.FromSeconds(2), TestContext.CancellationToken);
            Assert.IsTrue(connection.Disconnected.IsCompleted);
        }

        [TestMethod]
        public async Task CompletesWhenDisposeAsyncCalled()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(connection.Disconnected.IsCompleted);
        }

        [TestMethod]
        public async Task IsIdempotentOnDoubleCompleteAndDispose()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act — double CompleteReceive and one DisposeAsync; none should throw
            connection.CompleteReceive();
            connection.CompleteReceive();
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(connection.Disconnected.IsCompleted);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class DisconnectedToken : NearbyConnectionTests
    {
        [TestMethod]
        public void IsNotCanceled_WhileConnected()
        {
            // Arrange
            var connection = Create.Connection();

            // Assert
            Assert.IsFalse(connection.DisconnectedToken.IsCancellationRequested);
        }

        [TestMethod]
        public void IsCanceled_AfterCompleteReceive()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            connection.CompleteReceive();

            // Assert
            Assert.IsTrue(connection.DisconnectedToken.IsCancellationRequested);
        }

        [TestMethod]
        public async Task IsCanceled_AfterDisposeAsync()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(connection.DisconnectedToken.IsCancellationRequested);
        }

        // DisconnectedToken is public and consumers hold connection references past teardown, so the
        // backing CancellationTokenSource must NOT be disposed — reading the token after
        // DisposeAsync has to keep working rather than throw ObjectDisposedException.
        [TestMethod]
        public async Task RemainsReadable_AfterDisposeAsync()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert — reading the token and registering on it must not throw
            var token = connection.DisconnectedToken;
            Assert.IsTrue(token.IsCancellationRequested);
            using var registration = token.Register(static () => { });
        }

        [TestMethod]
        public async Task ReceiveAsync_ExitsLoop_WhenPeerDisconnectsMidEnumeration()
        {
            // Arrange
            var channel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = Create.Connection(receiveChannel: channel);
            channel.Writer.TryWrite(new NearbyBytesPayload([1]));

            // Act — disconnect from inside the loop after the first payload
            var received = new List<NearbyPayload>();
            await foreach (var payload in connection.ReceiveAsync(TestContext.CancellationToken))
            {
                received.Add(payload);
                connection.CompleteReceive();
            }

            // Assert — the loop terminated without the consumer owning a token at all
            Assert.HasCount(1, received);
        }

        // Pins the documented misuse. Passing DisconnectedToken to ReceiveAsync looks natural and is
        // wrong: ReadAllAsync observes cancellation on every iteration, so an already-cancelled token
        // throws and discards buffered payloads — the exact data loss the design must prevent. If
        // this ever stops throwing, the remarks on DisconnectedToken need revisiting.
        [TestMethod]
        public async Task ReceiveAsync_WithDisconnectedToken_ThrowsAndDiscardsBufferedPayloads()
        {
            // Arrange
            var channel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = Create.Connection(receiveChannel: channel);
            channel.Writer.TryWrite(new NearbyBytesPayload([1]));
            connection.CompleteReceive();

            // Act + Assert
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            {
                await foreach (var _ in connection.ReceiveAsync(connection.DisconnectedToken))
                {
                }
            });
        }

        public TestContext TestContext { get; set; }
    }
}
