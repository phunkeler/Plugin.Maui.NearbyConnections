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
        Func<byte[], CancellationToken, Task>? sendBytesFactory = null,
        Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task>? sendFileFactory = null,
        Func<ValueTask>? disposeFactory = null)
    {
        return new NearbyConnection(
            device ?? new NearbyDevice("peer-1", "Alice"),
            receiveChannel ?? Channel.CreateUnbounded<NearbyPayload>(),
            sendBytesFactory ?? ((_, _) => Task.CompletedTask),
            sendFileFactory ?? ((_, _, _) => Task.CompletedTask),
            disposeFactory ?? (() => ValueTask.CompletedTask));
    }

    [TestClass]
    public sealed class RemoteDeviceTests
    {
        [TestMethod]
        public void RemoteDevice_ReturnsConstructedDevice()
        {
            // Arrange
            var device = new NearbyDevice("peer-42", "Bob");
            var connection = CreateConnection(device: device);

            // Assert
            Assert.AreSame(device, connection.RemoteDevice);
        }
    }

    [TestClass]
    public sealed class SendAsyncBytesTests
    {
        [TestMethod]
        public async Task SendAsync_Bytes_DelegatesToSendBytesFactory()
        {
            // Arrange
            byte[]? captured = null;
            var connection = CreateConnection(
                sendBytesFactory: (data, _) => { captured = data; return Task.CompletedTask; });

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
                sendBytesFactory: (_, ct) => { capturedToken = ct; return Task.CompletedTask; });

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

            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                async () => await connection.SendAsync((byte[])null!));
        }
    }

    [TestClass]
    public sealed class SendAsyncFileTests
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

            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                async () => await connection.SendAsync((string)null!));
        }
    }

    [TestClass]
    public sealed class ReceiveAsyncTests
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

            // Act — start enumeration then cancel immediately (channel stays open)
            cts.Cancel();

            // TaskCanceledException is a subclass of OperationCanceledException
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () =>
                {
                    await foreach (var _ in connection.ReceiveAsync(cts.Token)) { }
                });
        }
    }

    [TestClass]
    public sealed class CompleteReceiveTests
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
    public sealed class DisposeAsyncTests
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

            // Start enumerating on a background task
            var enumerateTask = Task.Run(async () =>
            {
                await foreach (var _ in connection.ReceiveAsync(cts.Token)) { }
            }, cts.Token);

            // Act
            await connection.DisposeAsync();

            // Assert — enumeration completes because the channel writer is completed by DisposeAsync
            await enumerateTask.WaitAsync(cts.Token);
        }

        [TestMethod]
        public async Task TryWritePayload_AfterDispose_DoesNotThrow()
        {
            // Arrange
            var receiveChannel = Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var connection = CreateConnection(receiveChannel: receiveChannel);
            await connection.DisposeAsync();

            // Act & Assert — TryWritePayload after dispose silently drops
            connection.TryWritePayload(new BytesPayload([1, 2, 3]));
        }
    }
}
