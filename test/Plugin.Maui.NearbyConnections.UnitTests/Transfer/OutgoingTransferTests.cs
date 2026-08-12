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
        public void TokenCapturedBeforeAnUpdate_IsNeutralisedRatherThanFiringLate()
        {
            // Load-bearing detail, and not the obvious one. OnUpdate swaps _inactivityCts for a
            // fresh source and disposes the old one — and disposing a CancellationTokenSource
            // cancels its pending timer, so the old token never fires afterwards.
            //
            // This is what makes the reset safe for the platform code. Both platforms capture
            // InactivityToken exactly once into a linked CTS and then await; they never re-read it.
            // Had the old source kept its timer alive, that captured token would fire on the
            // ORIGINAL deadline and abort a transfer that was making perfectly good progress.
            //
            // If old.Dispose() is ever removed from OnUpdate — say, to "avoid disposing something
            // a caller might still hold" — this test fails and explains why that is unsafe.

            // Arrange
            var time = new FakeTimeProvider();
            using var transfer = Create.Transfer(time);
            var capturedEarly = transfer.InactivityToken;

            // Act
            time.Advance(TimeSpan.FromSeconds(9));
            transfer.OnUpdate(Create.ProgressUpdate(NearbyTransferStatus.InProgress));
            time.Advance(TimeSpan.FromSeconds(5)); // well past the original 10s deadline

            // Assert
            Assert.IsFalse(
                capturedEarly.IsCancellationRequested,
                "The old source's timer must be dead after Dispose, or a progressing transfer aborts.");
            Assert.IsFalse(
                transfer.InactivityToken.IsCancellationRequested,
                "The current token is only 5s into its fresh 10s deadline.");
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
