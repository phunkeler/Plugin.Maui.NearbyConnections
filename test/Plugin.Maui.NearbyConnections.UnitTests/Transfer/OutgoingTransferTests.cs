using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers <see cref="OutgoingTransfer"/>: the inactivity deadline and the terminal-state
/// transitions that end a <c>SendAsync</c> call.
/// </summary>
/// <remarks>
/// <see cref="OutgoingTransfer"/> takes a <see cref="TimeProvider"/> specifically so these can be
/// driven with <see cref="FakeTimeProvider"/> rather than a real ten-second wait. That seam existed
/// before these tests did and went unused.
/// </remarks>
[TestCategory("Connections")]
public class OutgoingTransferTests
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    static NearbyTransferProgress Update(NearbyTransferStatus status, long bytes = 0)
        => new(payloadId: 1, bytesTransferred: bytes, totalBytes: 100, status);

    static OutgoingTransfer CreateSut(
        FakeTimeProvider time,
        IProgress<NearbyTransferProgress>? progress = null,
        TimeSpan? timeout = null)
        => new(progress, timeout ?? Timeout, time);

    /// <summary>Captures reported progress so ordering and content can be asserted.</summary>
    sealed class RecordingProgress : IProgress<NearbyTransferProgress>
    {
        public List<NearbyTransferProgress> Reports { get; } = [];

        public void Report(NearbyTransferProgress value) => Reports.Add(value);
    }

    [TestClass]
    public sealed class InactivityTimeout : OutgoingTransferTests
    {
        [TestMethod]
        public void BeforeTheDeadline_TokenIsNotCancelled()
        {
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            time.Advance(Timeout - TimeSpan.FromMilliseconds(1));

            Assert.IsFalse(sut.InactivityToken.IsCancellationRequested);
        }

        [TestMethod]
        public void AtTheDeadline_TokenIsCancelled()
        {
            // This is what turns a silently stalled transfer into a NearbyTransferTimeoutException
            // instead of a SendAsync that never returns.
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            time.Advance(Timeout);

            Assert.IsTrue(sut.InactivityToken.IsCancellationRequested);
        }

        [TestMethod]
        public void ProgressUpdate_ResetsTheDeadline()
        {
            // The timeout measures inactivity, not total duration: a transfer making steady progress
            // must never time out, however long it runs.
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            for (var i = 0; i < 5; i++)
            {
                time.Advance(Timeout - TimeSpan.FromSeconds(1));
                sut.OnUpdate(Update(NearbyTransferStatus.InProgress, bytes: i * 10));

                Assert.IsFalse(
                    sut.InactivityToken.IsCancellationRequested,
                    $"Timed out after update {i} despite continuous progress.");
            }

            // Total elapsed time is far past the timeout, but no single gap ever was.
            Assert.IsTrue(time.GetUtcNow() > DateTimeOffset.UnixEpoch.AddSeconds(40));
        }

        [TestMethod]
        public void AfterAnUpdate_TheFullTimeoutIsAvailableAgain()
        {
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            time.Advance(TimeSpan.FromSeconds(9));
            sut.OnUpdate(Update(NearbyTransferStatus.InProgress));

            time.Advance(TimeSpan.FromSeconds(9));
            Assert.IsFalse(sut.InactivityToken.IsCancellationRequested, "The deadline was not reset.");

            time.Advance(TimeSpan.FromSeconds(1));
            Assert.IsTrue(sut.InactivityToken.IsCancellationRequested);
        }

        [TestMethod]
        public void InfiniteTimeout_NeverCancels()
        {
            // Documented escape hatch on TransferInactivityTimeout.
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time, timeout: System.Threading.Timeout.InfiniteTimeSpan);

            time.Advance(TimeSpan.FromDays(1));

            Assert.IsFalse(sut.InactivityToken.IsCancellationRequested);
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
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            var capturedEarly = sut.InactivityToken;

            time.Advance(TimeSpan.FromSeconds(9));
            sut.OnUpdate(Update(NearbyTransferStatus.InProgress));
            time.Advance(TimeSpan.FromSeconds(5)); // well past the original 10s deadline

            Assert.IsFalse(
                capturedEarly.IsCancellationRequested,
                "The old source's timer must be dead after Dispose, or a progressing transfer aborts.");
            Assert.IsFalse(
                sut.InactivityToken.IsCancellationRequested,
                "The current token is only 5s into its fresh 10s deadline.");
        }
    }

    [TestClass]
    public sealed class TerminalStates : OutgoingTransferTests
    {
        [TestMethod]
        public async Task Success_CompletesTheTask()
        {
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            sut.OnUpdate(Update(NearbyTransferStatus.Success));

            await sut.Completion;
            Assert.IsTrue(sut.Completion.IsCompletedSuccessfully);
        }

        [TestMethod]
        public async Task Failure_FaultsTheTask()
        {
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            sut.OnUpdate(Update(NearbyTransferStatus.Failure));

            var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.Completion);
            Assert.Contains("1", ex.Message, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task Canceled_CancelsTheTask()
        {
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            sut.OnUpdate(Update(NearbyTransferStatus.Canceled));

            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => sut.Completion);
        }

        [TestMethod]
        public void InProgress_LeavesTheTaskPending()
        {
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            sut.OnUpdate(Update(NearbyTransferStatus.InProgress));

            Assert.IsFalse(sut.Completion.IsCompleted);
        }

        [TestMethod]
        public async Task FirstTerminalStatusWins()
        {
            // TrySet* rather than Set*: a platform that reports Success then Failure must not throw
            // InvalidOperationException from inside a native callback.
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time);

            sut.OnUpdate(Update(NearbyTransferStatus.Success));
            sut.OnUpdate(Update(NearbyTransferStatus.Failure));
            sut.OnUpdate(Update(NearbyTransferStatus.Canceled));

            await sut.Completion;
            Assert.IsTrue(sut.Completion.IsCompletedSuccessfully);
        }
    }

    [TestClass]
    public sealed class ProgressReporting : OutgoingTransferTests
    {
        [TestMethod]
        public void EveryUpdate_IsForwardedInOrder()
        {
            var time = new FakeTimeProvider();
            var recorder = new RecordingProgress();
            using var sut = CreateSut(time, recorder);

            sut.OnUpdate(Update(NearbyTransferStatus.InProgress, bytes: 10));
            sut.OnUpdate(Update(NearbyTransferStatus.InProgress, bytes: 50));
            sut.OnUpdate(Update(NearbyTransferStatus.Success, bytes: 100));

            Assert.HasCount(3, recorder.Reports);
            CollectionAssert.AreEqual(
                new long[] { 10, 50, 100 },
                recorder.Reports.Select(r => r.BytesTransferred).ToArray());
        }

        [TestMethod]
        public void NullProgress_IsTolerated()
        {
            // progress is optional on every SendAsync overload.
            var time = new FakeTimeProvider();
            using var sut = CreateSut(time, progress: null);

            sut.OnUpdate(Update(NearbyTransferStatus.Success));

            Assert.IsTrue(sut.Completion.IsCompletedSuccessfully);
        }
    }

    [TestClass]
    public sealed class Disposal : OutgoingTransferTests
    {
        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            var time = new FakeTimeProvider();
            var sut = CreateSut(time);

            sut.Dispose();
            sut.Dispose();
        }

        [TestMethod]
        public void DisposeAfterTimeout_DoesNotThrow()
        {
            // The finally block in PlatformSendFileAsync disposes on the timeout path too.
            var time = new FakeTimeProvider();
            var sut = CreateSut(time);

            time.Advance(Timeout);
            sut.Dispose();
        }
    }
}
