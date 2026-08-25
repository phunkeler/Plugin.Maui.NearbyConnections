using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[Trait("Category", "Connections")]
public class NearbyConnectionTests
{
    public sealed class RemoteDevice : NearbyConnectionTests
    {
        [Fact]
        public void ReturnsConstructedDevice()
        {
            // Arrange
            var device = new NearbyDevice("peer-42", "Bob");
            var connection = Create.Connection(device: device);

            // Act
            var result = connection.RemoteDevice;

            // Assert
            Assert.Same(device, result);
        }
    }

    public sealed class SendAsyncBytes : NearbyConnectionTests
    {
        [Fact]
        public async Task SendAsync_Bytes_DelegatesToSendBytes()
        {
            // Arrange
            byte[]? captured = null;
            var connection = Create.Connection(
                sendBytes: (data, _) => { captured = data; return Task.CompletedTask; });
            var payload = new byte[] { 10, 20, 30 };

            // Act
            await connection.SendAsync(payload, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(payload, captured);
        }

        [Fact]
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
            Assert.Equal(cts.Token, capturedToken);
        }

        [Fact]
        public async Task SendAsync_NullBytes_ThrowsArgumentNullException()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            Func<Task> act = () => connection.SendAsync((byte[])null!, TestContext.Current.CancellationToken);

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>(act);
        }
    }

    public sealed class SendAsyncFile : NearbyConnectionTests
    {
        [Fact]
        public async Task SendAsync_File_DelegatesToSendFile()
        {
            // Arrange
            string? capturedUri = null;
            var connection = Create.Connection(
                sendFile: (uri, _, _) => { capturedUri = uri; return Task.CompletedTask; });

            // Act
            await connection.SendAsync("/path/to/file.txt", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("/path/to/file.txt", capturedUri);
        }

        [Fact]
        public async Task SendAsync_File_ForwardsProgress()
        {
            // Arrange
            IProgress<NearbyTransferProgress>? capturedProgress = null;
            var connection = Create.Connection(
                sendFile: (_, progress, _) => { capturedProgress = progress; return Task.CompletedTask; });
            var progress = new Progress<NearbyTransferProgress>();

            // Act
            await connection.SendAsync("/path/to/file.txt", progress, TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(progress, capturedProgress);
        }

        [Fact]
        public async Task SendAsync_NullFileUri_ThrowsArgumentNullException()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            async Task act() => await connection.SendAsync((string)null!, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>(act);
        }
    }

    public sealed class ReceiveAsync : NearbyConnectionTests
    {

        [Fact]
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
            Assert.Single(received);
            Assert.Same(payload, received[0]);
        }

        [Fact]
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
            Assert.Equal(3, received.Count);
            Assert.Same(p1, received[0]);
            Assert.Same(p2, received[1]);
            Assert.Same(p3, received[2]);
        }

        [Fact]
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
            await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        }

        [Fact]
        public async Task CalledTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            var connection = Create.Connection();
            connection.ReceiveAsync(TestContext.Current.CancellationToken); // first call — sets guard

            // Act
            Task Act() { connection.ReceiveAsync(TestContext.Current.CancellationToken); return Task.CompletedTask; }

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(Act);
        }

        [Fact]
        public async Task CalledAfterCancellation_ThrowsInvalidOperationException()
        {
            // Arrange
            var connection = Create.Connection();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            try { await foreach (var _ in connection.ReceiveAsync(cts.Token)) { } }
            catch (OperationCanceledException) { }

            // Act
            Task Act() { connection.ReceiveAsync(TestContext.Current.CancellationToken); return Task.CompletedTask; }

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(Act);
        }
    }

    public sealed class CompleteReceive : NearbyConnectionTests
    {
        [Fact]
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
            Assert.Empty(received);
        }
    }

    public sealed class DisposeAsync : NearbyConnectionTests
    {
        [Fact]
        public async Task CallsDispose()
        {
            // Arrange
            var disposed = false;
            var connection = Create.Connection(
                dispose: () => { disposed = true; return ValueTask.CompletedTask; });

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.True(disposed);
        }

        [Fact]
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
            Assert.True(enumerateTask.IsCompletedSuccessfully);
        }

        [Fact]
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
            Assert.False(receiveChannel.Reader.TryRead(out _));
        }

        // IsBeingConsumed is what lets the platform layer detect the silent-loss case: payloads
        // arriving on a connection whose ReceiveAsync was never called.
        [Fact]
        public void IsBeingConsumed_BeforeReceiveAsync_IsFalse()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            var consumed = connection.IsBeingConsumed;

            // Assert
            Assert.False(consumed);
        }

        // Pins the retention guarantee the whole late-consumer story rests on: the receive channel is
        // unbounded and TryWritePayload writes unconditionally, so payloads that arrive long before
        // anything calls ReceiveAsync are buffered, not dropped. A consumer that starts late drains
        // the entire backlog from connection-open, which is why the plugin needs no separate
        // payload-replay feature — only a reliable way to hand out the connection.
        [Fact]
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

            await foreach (var payload in connection.ReceiveAsync(TestContext.Current.CancellationToken))
            {
                received.Add(((NearbyBytesPayload)payload).Data[0]);
            }

            // Assert — every pre-subscription payload is delivered, in arrival order
            Assert.Equal(3, received.Count);
            Assert.Equal(1, received[0]);
            Assert.Equal(2, received[1]);
            Assert.Equal(3, received[2]);
        }

        [Fact]
        public void IsBeingConsumed_AfterReceiveAsync_IsTrue()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            _ = connection.ReceiveAsync(TestContext.Current.CancellationToken);

            // Assert — set by calling ReceiveAsync, without needing to enumerate it
            Assert.True(connection.IsBeingConsumed);
        }
    }

    public sealed class Disconnected : NearbyConnectionTests
    {
        [Fact]
        public async Task CompletesWhenCompleteReceiveCalled()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            connection.CompleteReceive();

            // Assert
            await connection.Disconnected.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.True(connection.Disconnected.IsCompleted);
        }

        [Fact]
        public async Task CompletesWhenDisposeAsyncCalled()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.True(connection.Disconnected.IsCompleted);
        }

        [Fact]
        public async Task IsIdempotentOnDoubleCompleteAndDispose()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act — double CompleteReceive and one DisposeAsync; none should throw
            connection.CompleteReceive();
            connection.CompleteReceive();
            await connection.DisposeAsync();

            // Assert
            Assert.True(connection.Disconnected.IsCompleted);
        }

        [Fact]
        public async Task LocalDispose_ReportsDisconnectedByLocal()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.Equal(NearbyEndReason.DisconnectedByLocal, await connection.Disconnected);
        }

        [Fact]
        public async Task PlatformRelease_ReportsDisconnected()
        {
            // The release path is how a remote close or a link loss reaches the connection.

            // Arrange
            var connection = Create.Connection();

            // Act
            connection.CompleteReceive();

            // Assert
            Assert.Equal(NearbyEndReason.Disconnected, await connection.Disconnected);
        }

        [Fact]
        public async Task FirstCompletionWins_LocalDisposeBeatsTheReleaseThatFollowsIt()
        {
            // A local dispose triggers the platform release, which completes the same source with
            // Disconnected — the reason recorded first must survive.

            // Arrange
            NearbyConnection? connection = null;
            connection = Create.Connection(dispose: () =>
            {
                connection!.CompleteReceive();
                return ValueTask.CompletedTask;
            });

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.Equal(NearbyEndReason.DisconnectedByLocal, await connection.Disconnected);
        }
    }

    public sealed class DisconnectedToken : NearbyConnectionTests
    {
        [Fact]
        public void IsNotCanceled_WhileConnected()
        {
            // Arrange
            var connection = Create.Connection();

            // Assert
            Assert.False(connection.DisconnectedToken.IsCancellationRequested);
        }

        [Fact]
        public void IsCanceled_AfterCompleteReceive()
        {
            // Arrange
            var connection = Create.Connection();

            // Act
            connection.CompleteReceive();

            // Assert
            Assert.True(connection.DisconnectedToken.IsCancellationRequested);
        }

        [Fact]
        public async Task IsCanceled_AfterDisposeAsync()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert
            Assert.True(connection.DisconnectedToken.IsCancellationRequested);
        }

        // DisconnectedToken is public and consumers hold connection references past teardown, so the
        // backing CancellationTokenSource must NOT be disposed — reading the token after
        // DisposeAsync has to keep working rather than throw ObjectDisposedException.
        [Fact]
        public async Task RemainsReadable_AfterDisposeAsync()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);

            // Act
            await connection.DisposeAsync();

            // Assert — reading the token and registering on it must not throw
            var token = connection.DisconnectedToken;
            Assert.True(token.IsCancellationRequested);
            using var registration = token.Register(static () => { });
        }

        [Fact]
        public async Task Register_AfterDisposeAsync_RunsCallbackInline()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);
            await connection.DisposeAsync();
            var ranOnRegisteringThread = false;
            var registeringThreadId = Environment.CurrentManagedThreadId;

            // Act
            using var registration = connection.DisconnectedToken.Register(
                () => ranOnRegisteringThread = Environment.CurrentManagedThreadId == registeringThreadId);

            // Assert
            Assert.True(ranOnRegisteringThread);
        }

        [Fact]
        public async Task ComposesIntoLinkedSource_AfterDisposeAsync()
        {
            // Arrange
            var connection = Create.Connection(dispose: () => ValueTask.CompletedTask);
            await connection.DisposeAsync();
            using var caller = new CancellationTokenSource();

            // Act
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                connection.DisconnectedToken, caller.Token);

            // Assert
            Assert.True(linked.Token.IsCancellationRequested);
        }

        [Fact]
        public async Task ReceiveAsync_ExitsLoop_WhenPeerDisconnectsMidEnumeration()
        {
            // Arrange
            var channel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = Create.Connection(receiveChannel: channel);
            channel.Writer.TryWrite(new NearbyBytesPayload([1]));

            // Act — disconnect from inside the loop after the first payload
            var received = new List<NearbyPayload>();
            await foreach (var payload in connection.ReceiveAsync(TestContext.Current.CancellationToken))
            {
                received.Add(payload);
                connection.CompleteReceive();
            }

            // Assert — the loop terminated without the consumer owning a token at all
            Assert.Single(received);
        }

        // Pins the documented misuse. Passing DisconnectedToken to ReceiveAsync looks natural and is
        // wrong: ReadAllAsync observes cancellation on every iteration, so an already-cancelled token
        // throws and discards buffered payloads — the exact data loss the design must prevent. If
        // this ever stops throwing, the remarks on DisconnectedToken need revisiting.
        [Fact]
        public async Task ReceiveAsync_WithDisconnectedToken_ThrowsAndDiscardsBufferedPayloads()
        {
            // Arrange
            var channel = Channel.CreateUnbounded<NearbyPayload>();
            var connection = Create.Connection(receiveChannel: channel);
            channel.Writer.TryWrite(new NearbyBytesPayload([1]));
            connection.CompleteReceive();

            // Act + Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await foreach (var _ in connection.ReceiveAsync(connection.DisconnectedToken))
                {
                }
            });
        }
    }
}
