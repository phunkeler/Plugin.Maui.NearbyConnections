namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Regression guard for <see cref="ConnectionLifecycle{TPending, TEvent}.EventsAsync"/>'s snapshot
/// materialization: <c>buildSnapshot</c> is evaluated and eagerly copied while <c>StateLock</c> is
/// still held, so a caller supplying a lazy LINQ query closed over the live
/// <c>PendingSnapshot</c>/<c>ActiveSnapshot</c> lists (as <c>NearbyAdvertiser</c>/
/// <c>NearbyDiscoverer</c> both do) is safe even if those lists mutate mid-enumeration. An earlier
/// draft of this refactor enumerated the lazy query outside the lock, which threw
/// <see cref="InvalidOperationException"/> ("Collection was modified") if a connection was
/// accepted/rejected/discovered while a subscriber was still draining the initial snapshot.
/// </summary>
[TestClass]
[TestCategory("ConnectionLifecycle")]
public sealed class ConnectionLifecycleAdversarialTests
{
    // Deterministic, no timing/race dependency: drives the async iterator one step at a time via
    // IAsyncEnumerator, so the mutation is injected at an exact, controlled point relative to
    // enumeration — between MoveNextAsync calls — rather than relying on real concurrency.
    [TestMethod]
    public async Task EventsAsync_SnapshotMutatedBetweenMoveNextCalls_DoesNotThrow()
    {
        // Arrange
        var lifecycle = new ConnectionLifecycle<string, string>();
        lock (lifecycle.StateLock)
        {
            lifecycle.PendingSnapshot.Add("a");
            lifecycle.PendingSnapshot.Add("b");
        }

        // Mirrors NearbyAdvertiser.EventsAsync's buildSnapshot lambda exactly: a lazy LINQ
        // projection over the live PendingSnapshot list, with no .ToList() materialization —
        // ConnectionLifecycle.EventsAsync is responsible for materializing it safely.
        var enumerable = lifecycle.EventsAsync(
            buildSnapshot: () => lifecycle.PendingSnapshot.Select(p => p + "-event"),
            synchronized: () => "synchronized");

        await using var enumerator = enumerable.GetAsyncEnumerator();

        // Act — consume the first snapshot item, then mutate the same list the snapshot was
        // built from between MoveNextAsync calls, exactly as AcceptAsync/RejectAsync/
        // RunLoopAsync would do concurrently in production.
        var movedFirst = await enumerator.MoveNextAsync();
        var firstItem = enumerator.Current;
        lock (lifecycle.StateLock)
        {
            lifecycle.PendingSnapshot.Add("c");
        }
        var movedSecond = await enumerator.MoveNextAsync();
        var secondItem = enumerator.Current;

        // Assert
        Assert.IsTrue(movedFirst);
        Assert.AreEqual("a-event", firstItem);
        Assert.IsTrue(movedSecond);
        Assert.AreEqual("b-event", secondItem);
    }
}
