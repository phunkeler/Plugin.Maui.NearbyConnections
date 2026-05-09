using System.Threading.Channels;
using Plugin.Maui.NearbyConnections;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Connections")]
public sealed class NearbyConnectionDisconnectedTests
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
    public sealed class Disconnected_CompletesWhenCompleteReceiveCalledTests
    {
        [TestMethod]
        public async Task Disconnected_CompletesWhenCompleteReceiveCalled()
        {
            // Arrange
            var connection = CreateConnection();

            // Act
            connection.CompleteReceive();

            // Assert — should not throw; completes within timeout
            await connection.Disconnected.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestClass]
    public sealed class Disconnected_CompletesWhenDisposeAsyncCalledTests
    {
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
    }

    [TestClass]
    public sealed class Disconnected_IsIdempotentOnDoubleCompleteAndDisposeTests
    {
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
