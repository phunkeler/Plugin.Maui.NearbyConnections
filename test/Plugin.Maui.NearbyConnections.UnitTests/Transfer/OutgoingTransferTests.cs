using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class OutgoingTransferTests
{
    [TestClass]
    public sealed class InactivityTimeout : OutgoingTransferTests
    {
        [TestMethod]
        public void BeforeTheDeadline_TokenIsNotCancelled()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            time.Advance(TimeSpan.FromSeconds(Create.TransferTimeoutSeconds) - TimeSpan.FromMilliseconds(1));

            // Assert
            Assert.IsFalse(transfer.InactivityToken.IsCancellationRequested);
        }

        [TestMethod]
        public void AtTheDeadline_TokenIsCancelled()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            time.Advance(TimeSpan.FromSeconds(Create.TransferTimeoutSeconds));

            // Assert
            Assert.IsTrue(transfer.InactivityToken.IsCancellationRequested);
        }

        [TestMethod]
        public void ProgressUpdate_ResetsTheDeadline()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            for (var i = 0; i < 5; i++)
            {
                time.Advance(TimeSpan.FromSeconds(Create.TransferTimeoutSeconds) - TimeSpan.FromSeconds(1));
                transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress, bytes: i * 10));

                Assert.IsFalse(
                    transfer.InactivityToken.IsCancellationRequested,
                    $"Timed out after update {i} despite continuous progress.");
            }

            // Assert — total elapsed time is far past the timeout, but no single gap ever was.
            Assert.IsGreaterThan(DateTimeOffset.UnixEpoch.AddSeconds(40), time.GetUtcNow());
        }

        [TestMethod]
        public void UpdateAfterTheDeadline_LeavesTheTokenCancelled()
        {
            // A stalled transfer times out, then a late platform callback lands — the transfer has
            // not been disposed yet, so OnUpdate still runs. Rescheduling an already-cancelled
            // source must not throw and must not un-cancel it: the caller has already been handed
            // the timeout exception, and cancellation does not reverse.

            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);
            time.Advance(TimeSpan.FromSeconds(Create.TransferTimeoutSeconds));

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress));

            // Assert
            Assert.IsTrue(transfer.InactivityToken.IsCancellationRequested);
        }

        [TestMethod]
        public void InfiniteTimeout_NeverCancels()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time, timeout: System.Threading.Timeout.InfiniteTimeSpan);

            // Act
            time.Advance(TimeSpan.FromDays(1));

            // Assert
            Assert.IsFalse(transfer.InactivityToken.IsCancellationRequested);
        }

        [TestMethod]
        public void TokenCapturedBeforeAnUpdate_DoesNotFireOnTheOriginalDeadline()
        {
            // Both platforms read InactivityToken exactly once, into a linked source, and then
            // await. They never re-read it. So a reset must not let the captured token fire on the
            // deadline it was created with, or a transfer making good progress aborts.

            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);
            var capturedEarly = transfer.InactivityToken;

            // Act
            time.Advance(TimeSpan.FromSeconds(9));
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress));
            time.Advance(TimeSpan.FromSeconds(5)); // past the original 10s deadline

            // Assert
            Assert.IsFalse(
                capturedEarly.IsCancellationRequested,
                "The reset must move the deadline, or a progressing transfer aborts.");
        }

        [TestMethod]
        public void TokenCapturedBeforeAnUpdate_StillFiresOnTheResetDeadline()
        {
            // The other half of the rule above, and the one that was missing. Moving the deadline
            // must not retire the captured token altogether: the platform code is awaiting on it,
            // so a token that can never fire again turns a stalled transfer into a permanent hang.
            // This is termination guarantee 1 in AGENTS.md — SendAsync is bounded by
            // TransferInactivityTimeout.
            //
            // The original implementation swapped in a fresh source and disposed the old one, which
            // satisfied the sibling test above by neutralising the captured token rather than by
            // rescheduling it. That is why both halves are asserted, and separately.

            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);
            var capturedEarly = transfer.InactivityToken;

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress));
            time.Advance(TimeSpan.FromSeconds(Create.TransferTimeoutSeconds));

            // Assert
            Assert.IsTrue(
                capturedEarly.IsCancellationRequested,
                "A stalled transfer must still time out on the token the platform captured.");
        }
    }

    [TestClass]
    public sealed class TerminalStates : OutgoingTransferTests
    {
        [TestMethod]
        public async Task Success_CompletesTheTask()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Success));

            // Assert
            await transfer.Completion;
            Assert.IsTrue(transfer.Completion.IsCompletedSuccessfully);
        }

        [TestMethod]
        public async Task Failure_FaultsTheTask()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Failure));

            // Assert
            var ex = await Assert.ThrowsExactlyAsync<NearbyTransferException>(() => transfer.Completion);
            Assert.Contains("1", ex.Message, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task Canceled_CancelsTheTask()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Canceled));

            // Assert
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => transfer.Completion);
        }

        [TestMethod]
        public void InProgress_LeavesTheTaskPending()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress));

            // Assert
            Assert.IsFalse(transfer.Completion.IsCompleted);
        }

        [TestMethod]
        public async Task FirstTerminalStatusWins()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Success));
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Failure));
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Canceled));

            // Assert
            await transfer.Completion;
            Assert.IsTrue(transfer.Completion.IsCompletedSuccessfully);
        }
    }

    [TestClass]
    public sealed class ProgressReporting : OutgoingTransferTests
    {
        [TestMethod]
        public void EveryUpdate_IsForwardedInOrder()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var recorder = new RecordingProgress();
            using var transfer = Create.Transfer(time, recorder);

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress, bytes: 10));
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress, bytes: 50));
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Success, bytes: 100));

            // Assert
            Assert.HasCount(3, recorder.Reports);
            Assert.AreSequenceEqual(
                new long[] { 10, 50, 100 },
                recorder.Reports.Select(r => r.BytesTransferred).ToArray());
        }

        [TestMethod]
        public void NullProgress_IsTolerated()
        {
            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time, progress: null);

            // Act
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.Success));

            // Assert
            Assert.IsTrue(transfer.Completion.IsCompletedSuccessfully);
        }
    }

    [TestClass]
    public sealed class Disposal : OutgoingTransferTests
    {
        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var transfer = Create.Transfer(time);

            // Act
            transfer.Dispose();
            transfer.Dispose();
        }

        [TestMethod]
        public void DisposeAfterTimeout_DoesNotThrow()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var transfer = Create.Transfer(time);

            // Act
            time.Advance(TimeSpan.FromSeconds(Create.TransferTimeoutSeconds));
            transfer.Dispose();
        }
    }
}
