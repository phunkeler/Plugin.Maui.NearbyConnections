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
}
