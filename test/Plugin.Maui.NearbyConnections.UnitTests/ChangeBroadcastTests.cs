namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Tests for <see cref="ChangeBroadcast{T}"/>, the fan-out behind every broadcast stream in the
/// library. Asserted directly rather than through <see cref="NearbyDeviceRegistry"/>, because
/// watcher release is not observable from a consumer-visible surface: an abandoned watcher fails
/// silently, by buffering forever rather than by misbehaving.
/// </summary>
[TestCategory("Devices")]
public class ChangeBroadcastTests
{
    [TestClass]
    public sealed class WatcherRelease : ChangeBroadcastTests
    {
        // The regression this file exists for. Subscribe runs eagerly in GetAsyncEnumerator, so an
        // enumerator disposed without a single read must still release its watcher. When the
        // unsubscribe lived in the draining iterator's `finally`, it did not: an async iterator body
        // never starts until the first MoveNextAsync, so the watcher stayed registered for the life
        // of the session and every later Publish wrote into a channel nothing drained.
        [TestMethod]
        public async Task EnumeratorDisposedWithoutReading_ReleasesItsWatcher()
        {
            // Arrange
            var broadcast = new ChangeBroadcast<int>();
            var enumerator = broadcast.Stream.GetAsyncEnumerator(TestContext.CancellationToken);

            // Act
            await enumerator.DisposeAsync();

            // Assert
            Assert.AreEqual(0, broadcast.WatcherCount);
        }

        [TestMethod]
        public async Task EnumeratorDisposedAfterReading_ReleasesItsWatcher()
        {
            // Arrange
            var broadcast = new ChangeBroadcast<int>();
            var enumerator = broadcast.Stream.GetAsyncEnumerator(TestContext.CancellationToken);
            broadcast.Publish(1);
            await enumerator.MoveNextAsync();

            // Act
            await enumerator.DisposeAsync();

            // Assert
            Assert.AreEqual(0, broadcast.WatcherCount);
        }

        [TestMethod]
        public async Task DisposingTwice_ReleasesTheWatcherOnce()
        {
            // Arrange
            var broadcast = new ChangeBroadcast<int>();
            var enumerator = broadcast.Stream.GetAsyncEnumerator(TestContext.CancellationToken);
            await enumerator.DisposeAsync();

            // Act
            await enumerator.DisposeAsync();

            // Assert
            Assert.AreEqual(0, broadcast.WatcherCount);
        }

        // One abandoned enumeration must not take its siblings' watchers with it.
        [TestMethod]
        public async Task DisposingOneEnumerator_LeavesTheOtherSubscribed()
        {
            // Arrange
            var broadcast = new ChangeBroadcast<int>();
            var abandoned = broadcast.Stream.GetAsyncEnumerator(TestContext.CancellationToken);
            var kept = broadcast.Stream.GetAsyncEnumerator(TestContext.CancellationToken);

            // Act
            await abandoned.DisposeAsync();

            // Assert
            Assert.AreEqual(1, broadcast.WatcherCount);

            broadcast.Publish(7);
            Assert.IsTrue(await kept.MoveNextAsync());
            Assert.AreEqual(7, kept.Current);

            await kept.DisposeAsync();
        }

        // Eager subscribe is the other half of the contract, and the two are easy to break in
        // opposite directions: moving the unsubscribe out of the iterator must not move the
        // subscribe in with it.
        [TestMethod]
        public async Task ChangePublishedBeforeTheFirstRead_IsStillDelivered()
        {
            // Arrange
            var broadcast = new ChangeBroadcast<int>();
            var enumerator = broadcast.Stream.GetAsyncEnumerator(TestContext.CancellationToken);

            // Act
            broadcast.Publish(42);

            // Assert
            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.AreEqual(42, enumerator.Current);

            await enumerator.DisposeAsync();
        }

        public TestContext TestContext { get; set; }
    }
}
