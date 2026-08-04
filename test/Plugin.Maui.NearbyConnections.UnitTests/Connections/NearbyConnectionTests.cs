using System.Threading.Channels;
using Plugin.Maui.NearbyConnections;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Connections")]
public sealed class NearbyConnectionTests
{
    static NearbyConnection CreateConnection(
        NearbyDevice? device = null,
        Channel<NearbyPayload>? receiveChannel = null,
        Func<byte[], CancellationToken, ValueTask>? sendBytesFactory = null,
        Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task>? sendFileFactory = null,
        Func<ValueTask>? disposeFactory = null)
    {
        return new NearbyConnection(
            device ?? new NearbyDevice("peer-1", "Alice"),
            receiveChannel ?? Channel.CreateUnbounded<NearbyPayload>(),
            sendBytesFactory ?? ((_, _) => ValueTask.CompletedTask),
            sendFileFactory ?? ((_, _, _) => Task.CompletedTask),
            disposeFactory ?? (() => ValueTask.CompletedTask));
    }

    [TestClass]
    public sealed class RemoteDevice
    {
        [TestMethod]
        public void RemoteDevice_ReturnsConstructedDevice()
        {
            // Arrange
            var device = new NearbyDevice("peer-42", "Bob");
            var connection = CreateConnection(device: device);

            // Act
            var result = connection.RemoteDevice;

            // Assert
            Assert.AreSame(device, result);
        }
    }

    [TestClass]
    public sealed class SendAsyncBytes
    {
        [TestMethod]
        public async Task SendAsync_Bytes_DelegatesToSendBytesFactory()
        {
            // Arrange
            byte[]? captured = null;
            var connection = CreateConnection(
                sendBytesFactory: (data, _) => { captured = data; return ValueTask.CompletedTask; });

            var payload = new byte[] { 10, 20, 30 };

            // Act
            await connection.SendAsync(payload);

            // Assert
            Assert.IsNotNull(captured);
            CollectionAssert.AreEqual(payload, captured);
        }

        [TestMethod]
        public async Task SendAsync_Bytes_ForwardsCancellationToken()
        {
            // Arrange
            CancellationToken capturedToken = default;
            var connection = CreateConnection(
                sendBytesFactory: (_, ct) => { capturedToken = ct; return ValueTask.CompletedTask; });

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
            var connection = CreateConnection();

            // Act
            Func<Task> act = () => connection.SendAsync((byte[])null!).AsTask();

            // Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(act);
        }
    }

    [TestClass]
    public sealed class SendAsyncFile
    {
        [TestMethod]
        public async Task SendAsync_File_DelegatesToSendFileFactory()
        {
            // Arrange
            string? capturedUri = null;
            var connection = CreateConnection(
                sendFileFactory: (uri, _, _) => { capturedUri = uri; return Task.CompletedTask; });

            // Act
            await connection.SendAsync("/path/to/file.txt");

            // Assert
            Assert.AreEqual("/path/to/file.txt", capturedUri);
        }

        [TestMethod]
        public async Task SendAsync_File_ForwardsProgress()
        {
            // Arrange
            IProgress<NearbyTransferProgress>? capturedProgress = null;
            var connection = CreateConnection(
                sendFileFactory: (_, progress, _) => { capturedProgress = progress; return Task.CompletedTask; });

            var progress = new Progress<NearbyTransferProgress>();

            // Act
            await connection.SendAsync("/path/to/file.txt", progress);

            // Assert
            Assert.AreSame(progress, capturedProgress);
        }

        [TestMethod]
        public async Task SendAsync_NullFileUri_ThrowsArgumentNullException()
        {
            // Arrange
            var connection = CreateConnection();

            // Act
            Func<Task> act = async () => await connection.SendAsync((string)null!);

            // Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(act);
        }
    }

    [TestClass]
    public sealed class ReceiveAsync
    {

        [TestMethod]
        public async Task ReceiveAsync_WritePayload_YieldsPayload()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = CreateConnection(receiveChannel: receiveChannel);

            var payload = new BytesPayload([1, 2, 3]);

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
        public async Task ReceiveAsync_MultiplePayloads_YieldsInOrder()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = CreateConnection(receiveChannel: receiveChannel);

            var p1 = new BytesPayload([1]);
            var p2 = new BytesPayload([2]);
            var p3 = new BytesPayload([3]);

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
        public async Task ReceiveAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = CreateConnection(receiveChannel: receiveChannel);
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
        public async Task ReceiveAsync_CalledTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            var connection = CreateConnection();
            connection.ReceiveAsync(); // first call — sets guard

            // Act
            Task Act() { connection.ReceiveAsync(); return Task.CompletedTask; }

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(Act);
        }

        [TestMethod]
        public async Task ReceiveAsync_CalledAfterCancellation_ThrowsInvalidOperationException()
        {
            // Arrange
            var connection = CreateConnection();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            try { await foreach (var _ in connection.ReceiveAsync(cts.Token)) { } }
            catch (OperationCanceledException) { }

            // Act
            Task Act() { connection.ReceiveAsync(); return Task.CompletedTask; }

            // Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(Act);
        }
    }

    [TestClass]
    public sealed class CompleteReceive
    {
        [TestMethod]
        public async Task CompleteReceive_CompletesReceiveEnumerable()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = CreateConnection(receiveChannel: receiveChannel);

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
    public sealed class DisposeAsync
    {
        [TestMethod]
        public async Task DisposeAsync_CallsDisposeFactory()
        {
            // Arrange
            var disposed = false;
            var connection = CreateConnection(
                disposeFactory: () => { disposed = true; return ValueTask.CompletedTask; });

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(disposed);
        }

        [TestMethod]
        public async Task DisposeAsync_CompletesReceiveEnumerable()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = CreateConnection(receiveChannel: receiveChannel);

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
            var connection = CreateConnection(receiveChannel: receiveChannel);
            await connection.DisposeAsync();

            // Act
            connection.TryWritePayload(new BytesPayload([1, 2, 3]));

            // Assert — payload silently dropped; channel writer is completed so nothing was queued
            Assert.IsFalse(receiveChannel.Reader.TryRead(out _));
        }
    }

    // ===========================================================================
    // Disconnected
    // ===========================================================================
    [TestClass]
    public sealed class Disconnected
    {
        [TestMethod]
        public async Task Disconnected_CompletesWhenCompleteReceiveCalled()
        {
            // Arrange
            var connection = CreateConnection();

            // Act
            connection.CompleteReceive();

            // Assert
            await connection.Disconnected.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(connection.Disconnected.IsCompleted);
        }

        [TestMethod]
        public async Task Disconnected_CompletesWhenDisposeAsyncCalled()
        {
            // Arrange
            var connection = CreateConnection(disposeFactory: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(connection.Disconnected.IsCompleted);
        }

        [TestMethod]
        public async Task Disconnected_IsIdempotentOnDoubleCompleteAndDispose()
        {
            // Arrange
            var connection = CreateConnection(disposeFactory: () => ValueTask.CompletedTask);

            // Act — double CompleteReceive and one DisposeAsync; none should throw
            connection.CompleteReceive();
            connection.CompleteReceive();
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(connection.Disconnected.IsCompleted);
        }
    }

    // ===========================================================================
    // DisconnectedToken
    // ===========================================================================
    [TestClass]
    public sealed class DisconnectedToken
    {
        [TestMethod]
        public void DisconnectedToken_IsNotCanceled_WhileConnected()
        {
            // Arrange
            var connection = CreateConnection();

            // Assert
            Assert.IsFalse(connection.DisconnectedToken.IsCancellationRequested);
        }

        [TestMethod]
        public void DisconnectedToken_IsCanceled_AfterCompleteReceive()
        {
            // Arrange
            var connection = CreateConnection();

            // Act
            connection.CompleteReceive();

            // Assert
            Assert.IsTrue(connection.DisconnectedToken.IsCancellationRequested);
        }

        [TestMethod]
        public async Task DisconnectedToken_IsCanceled_AfterDisposeAsync()
        {
            // Arrange
            var connection = CreateConnection(disposeFactory: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.IsTrue(connection.DisconnectedToken.IsCancellationRequested);
        }

        // DisconnectedToken is public and consumers hold connection references past teardown, so the
        // backing CancellationTokenSource must NOT be disposed — reading the token after
        // DisposeAsync has to keep working rather than throw ObjectDisposedException.
        [TestMethod]
        public async Task DisconnectedToken_RemainsReadable_AfterDisposeAsync()
        {
            // Arrange
            var connection = CreateConnection(disposeFactory: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert — reading the token and registering on it must not throw
            var token = connection.DisconnectedToken;
            Assert.IsTrue(token.IsCancellationRequested);
            using var registration = token.Register(static () => { });
        }

        // The core guarantee: completing the writer — not cancelling a token — is what ends the
        // receive loop, so payloads buffered immediately before the disconnect are still delivered.
        // This is PayloadWrittenBeforeDisconnect_IsNotLost expressed at the NearbyConnection level.
        [TestMethod]
        public async Task ReceiveAsync_DeliversBufferedPayloads_ThenCompletes_AfterDisconnect()
        {
            // Arrange
            var channel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = CreateConnection(receiveChannel: channel);

            channel.Writer.TryWrite(new BytesPayload([1]));
            channel.Writer.TryWrite(new BytesPayload([2]));

            // Act — disconnect with payloads still buffered, then consume with no token
            connection.CompleteReceive();

            var received = new List<NearbyPayload>();
            await foreach (var payload in connection.ReceiveAsync())
            {
                received.Add(payload);
            }

            // Assert — both payloads survive the disconnect and the loop ends on its own
            Assert.HasCount(2, received);
            Assert.IsTrue(connection.DisconnectedToken.IsCancellationRequested);
        }

        [TestMethod]
        public async Task ReceiveAsync_ExitsLoop_WhenPeerDisconnectsMidEnumeration()
        {
            // Arrange
            var channel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = CreateConnection(receiveChannel: channel);
            channel.Writer.TryWrite(new BytesPayload([1]));

            // Act — disconnect from inside the loop after the first payload
            var received = new List<NearbyPayload>();
            await foreach (var payload in connection.ReceiveAsync())
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
            var connection = CreateConnection(receiveChannel: channel);
            channel.Writer.TryWrite(new BytesPayload([1]));
            connection.CompleteReceive();

            // Act + Assert
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            {
                await foreach (var _ in connection.ReceiveAsync(connection.DisconnectedToken))
                {
                }
            });
        }
    }
}
