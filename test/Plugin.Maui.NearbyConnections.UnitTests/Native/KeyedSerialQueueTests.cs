namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the queue that orders per-peer work and that release and disposal drain.
/// </summary>
/// <remarks>
/// These tests replace a device test that asserted the same ordering on a real emulator. That test
/// could only observe the order inside a timing window, and the Android API 24 leg lost the race on
/// 2026-08-24. The queue is pure BCL, so every item here is gated on a
/// <see cref="TaskCompletionSource"/> and the result does not depend on timing.
/// </remarks>
[Trait("Category", "Connections")]
public sealed class KeyedSerialQueueTests
{
    [Fact]
    public async Task TwoItemsSharingAKeyRunInOrder()
    {
        // The ordering guarantee. On Android the first item is a file copy that is still writing,
        // and the second must not read the same endpoint's state while it runs.

        // Arrange
        var queue = Create.KeyedSerialQueue();
        var firstGate = Create.Gate();
        var firstStarted = Create.Gate();
        var secondStarted = Create.Gate();

        var first = queue.Enqueue("peer-1", () =>
        {
            firstStarted.SetResult();

            return firstGate.Task;
        });

        var second = queue.Enqueue("peer-1", () =>
        {
            secondStarted.SetResult();

            return Task.CompletedTask;
        });

        // Wait for the first item to reach the gate. Reading the flag before that point tests
        // thread pool latency instead of the queue.
        await firstStarted.Task;

        // Act
        var secondStartedWhileFirstWasGated = secondStarted.Task.IsCompleted;
        firstGate.SetResult();
        await second;

        // Assert
        Assert.False(secondStartedWhileFirstWasGated);
        Assert.True(secondStarted.Task.IsCompleted);
        Assert.True(first.IsCompleted);
    }

    [Fact]
    public void EnqueueDoesNotRunTheWorkBeforeReturning()
    {
        // The defect this queue fixes. The Android caller is a Google Mobile Services callback that
        // may arrive on the main thread, and the work it queues opens and reads files. Work that
        // runs before Enqueue returns runs on that callback thread.
        //
        // This asserts that the caller is not blocked, not which thread the work lands on. Comparing
        // thread ids fails intermittently, because the thread pool may reuse the calling thread once
        // that thread is free.

        // Arrange
        var queue = Create.KeyedSerialQueue();
        var gate = Create.Gate();
        var workStarted = false;

        // Act
        var queued = queue.Enqueue("peer-1", () =>
        {
            workStarted = true;

            return gate.Task;
        });

        // Assert
        Assert.False(workStarted);
        Assert.False(queued.IsCompleted);

        gate.SetResult();
    }

    [Fact]
    public async Task DifferentKeysRunIndependently()
    {
        // One slow peer must not hold up another peer's payloads.

        // Arrange
        var queue = Create.KeyedSerialQueue();
        var blockedGate = Create.Gate();

        _ = queue.Enqueue("peer-1", () => blockedGate.Task);

        // Act
        var other = queue.Enqueue("peer-2", () => Task.CompletedTask);
        await other;

        // Assert
        Assert.True(other.IsCompleted);

        blockedGate.SetResult();
    }

    [Fact]
    public async Task ThrownWorkReachesTheErrorHandlerAndTheKeyKeepsRunning()
    {
        // A failed copy must not stop the payloads behind it, and it must not fault a task the
        // drain awaits.

        // Arrange
        var failures = new List<(string Key, Exception Error)>();
        var queue = Create.KeyedSerialQueue((key, ex) => failures.Add((key, ex)));
        var thrown = new InvalidOperationException("copy failed");
        var secondRan = false;

        // Act
        var failing = queue.Enqueue("peer-1", () => Task.FromException(thrown));
        var following = queue.Enqueue("peer-1", () =>
        {
            secondRan = true;

            return Task.CompletedTask;
        });

        await following;

        // Assert
        Assert.Single(failures);
        Assert.Equal("peer-1", failures[0].Key);
        Assert.Same(thrown, failures[0].Error);
        Assert.True(secondRan);
        Assert.False(failing.IsFaulted);
    }

    [Fact]
    public async Task DrainingAnUnknownKeyCompletesImmediately()
    {
        // Releasing a peer that received no payloads is the common case.

        // Arrange
        var queue = Create.KeyedSerialQueue();

        // Act
        var drained = await queue.DrainAsync("peer-nobody", TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(drained);
    }

    [Fact]
    public async Task DrainingOneKeyWaitsForItsQueuedWork()
    {
        // Arrange
        var queue = Create.KeyedSerialQueue();
        var gate = Create.Gate();
        var workFinished = false;

        _ = queue.Enqueue("peer-1", async () =>
        {
            await gate.Task;
            workFinished = true;
        });

        // Act
        var drain = queue.DrainAsync("peer-1", TimeSpan.FromSeconds(30));
        var finishedBeforeTheGateOpened = drain.IsCompleted;
        gate.SetResult();
        var drained = await drain;

        // Assert
        Assert.False(finishedBeforeTheGateOpened);
        Assert.True(drained);
        Assert.True(workFinished);
    }

    [Fact]
    public async Task DrainingAnEmptyQueueCompletesImmediately()
    {
        // Arrange
        var queue = Create.KeyedSerialQueue();

        // Act
        var drained = await queue.DrainAllAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(drained);
    }

    [Fact]
    public async Task DrainingEveryKeyWaitsForAllOfThem()
    {
        // Disposal sweeps the staging directory next, so it must wait for every peer's copy.

        // Arrange
        var queue = Create.KeyedSerialQueue();
        var firstGate = Create.Gate();
        var secondGate = Create.Gate();

        _ = queue.Enqueue("peer-1", () => firstGate.Task);
        _ = queue.Enqueue("peer-2", () => secondGate.Task);

        // Act
        var drain = queue.DrainAllAsync(TimeSpan.FromSeconds(30));
        firstGate.SetResult();
        var finishedAfterOnlyOneKey = drain.IsCompleted;
        secondGate.SetResult();
        var drained = await drain;

        // Assert
        Assert.False(finishedAfterOnlyOneKey);
        Assert.True(drained);
    }

    [Fact]
    public async Task DrainingReportsFalseWhenTheBoundElapses()
    {
        // The bound is what stops a stuck native read from turning a release or a disposal into a
        // hang. Both drains share one wait helper, so covering the disposal-wide one covers both.
        // Disposal logs KeyCount when this happens, so assert on the value it would report.

        // Arrange
        var queue = Create.KeyedSerialQueue();
        var gate = Create.Gate();

        _ = queue.Enqueue("peer-1", () => gate.Task);

        // Act
        var drained = await queue.DrainAllAsync(TimeSpan.FromMilliseconds(10));

        // Assert
        Assert.False(drained);
        Assert.Equal(1, queue.KeyCount);

        gate.SetResult();
    }
}
