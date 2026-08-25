namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Tests for <see cref="DeviceRegistry"/>, the thread-safe device store that replaced the
/// dispatcher-marshalled <c>ObservableCollection</c>.
/// </summary>
[Trait("Category", "Devices")]
public class DeviceRegistryTests
{
    public sealed class Membership : DeviceRegistryTests
    {
        [Fact]
        public void AddIfAbsent_NewDevice_AddsIt()
        {
            // Arrange
            var registry = new DeviceRegistry();
            var device = Create.Device("a");

            // Act
            var result = registry.AddIfAbsent(device);

            // Assert
            Assert.Same(device, result);
            Assert.Single(registry);
        }

        // The rediscovery case: a connected device seen again by discovery must not be reset to the
        // freshly-constructed Visible snapshot the platform hands over.
        [Fact]
        public void AddIfAbsent_ExistingDevice_KeepsTheIncumbent()
        {
            // Arrange
            var registry = new DeviceRegistry();
            var connected = Create.Device("a", status: NearbyDeviceStatus.Connected);
            registry.AddIfAbsent(connected);

            // Act
            var result = registry.AddIfAbsent(Create.Device("a"));

            // Assert
            Assert.Same(connected, result);
            Assert.Equal(NearbyDeviceStatus.Connected, registry[0].Status);
            Assert.Single(registry);
        }

        [Fact]
        public void Remove_AbsentDevice_ReturnsFalse()
        {
            // Arrange
            var registry = new DeviceRegistry();

            // Act
            var result = registry.Remove("nope");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveWhere_RemovesOnlyMatches()
        {
            // Arrange
            var registry = new DeviceRegistry();
            registry.AddIfAbsent(Create.Device("visible"));
            registry.AddIfAbsent(Create.Device("connected", status: NearbyDeviceStatus.Connected));

            // Act
            registry.RemoveWhere(d => d.Status is NearbyDeviceStatus.Visible);

            // Assert
            Assert.Single(registry);
            Assert.Equal("connected", registry[0].Id);
        }

        [Fact]
        public void Update_AbsentDevice_ReturnsNull()
        {
            // Arrange
            var registry = new DeviceRegistry();

            // Act
            var result = registry.Update("nope", d => d with { Status = NearbyDeviceStatus.Connected });

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Update_ReplacesTheStoredSnapshot()
        {
            // Arrange
            var registry = new DeviceRegistry();
            registry.AddIfAbsent(Create.Device("a"));

            // Act
            var result = registry.Update("a", d => d with { Status = NearbyDeviceStatus.Connected });

            // Assert
            Assert.Equal(NearbyDeviceStatus.Connected, result!.Status);
            Assert.Equal(NearbyDeviceStatus.Connected, registry[0].Status);
        }

        // Enumeration must never throw for concurrent modification — the whole reason reads go
        // through an immutable snapshot rather than the dictionary.
        [Fact]
        public void Enumerating_WhileMutating_DoesNotThrow()
        {
            // Arrange
            var registry = new DeviceRegistry();
            registry.AddIfAbsent(Create.Device("a"));

            // Act
            var enumerated = 0;

            foreach (var _ in registry)
            {
                registry.AddIfAbsent(Create.Device($"added-{enumerated}"));
                enumerated++;
            }

            // Assert
            // The snapshot taken at loop start must not grow underneath it.
            Assert.Equal(1, enumerated);
            Assert.Equal(2, registry.Count);
        }
    }

    public sealed class Changes : DeviceRegistryTests
    {
        [Fact]
        public async Task Add_PublishesAdded()
        {
            // Arrange
            var registry = new DeviceRegistry();
            await using var watch = new ChangeRecorder(registry.Changes);

            // Act
            registry.AddIfAbsent(Create.Device("a"));

            // Assert
            await watch.WaitForAsync(1);
            var changes = watch.Changes;
            Assert.Single(changes);
            Assert.Equal(NearbyDeviceChangeAction.Added, changes[0].Action);
            Assert.Equal("a", changes[0].Device.Id);
        }

        [Fact]
        public async Task AddIfAbsent_WhenAlreadyPresent_PublishesNothing()
        {
            // Arrange
            var registry = new DeviceRegistry();
            registry.AddIfAbsent(Create.Device("a"));
            await using var watch = new ChangeRecorder(registry.Changes);

            // Act
            registry.AddIfAbsent(Create.Device("a"));

            // Assert
            await watch.WaitForAsync(0);
            var changes = watch.Changes;
            // A no-op add must not wake every watcher.
            Assert.Empty(changes);
        }

        // Suppressing no-op updates is what keeps a bound row from re-rendering on every redundant
        // platform callback; the platforms re-report unchanged state routinely.
        [Fact]
        public async Task Update_ThatChangesNothing_PublishesNothing()
        {
            // Arrange
            var registry = new DeviceRegistry();
            registry.AddIfAbsent(Create.Device("a"));
            await using var watch = new ChangeRecorder(registry.Changes);

            // Act
            registry.Update("a", d => d);

            // Assert
            await watch.WaitForAsync(0);
            var changes = watch.Changes;
            Assert.Empty(changes);
        }

        [Fact]
        public async Task RemoveWhere_PublishesOneChangePerDevice()
        {
            // Arrange
            var registry = new DeviceRegistry();
            registry.AddIfAbsent(Create.Device("a"));
            registry.AddIfAbsent(Create.Device("b"));
            await using var watch = new ChangeRecorder(registry.Changes);

            // Act
            registry.Clear();

            // Assert
            await watch.WaitForAsync(2);
            var changes = watch.Changes;
            Assert.Equal(2, changes.Count);
            Assert.True(changes.All(c => c.Action is NearbyDeviceChangeAction.Removed));
        }

        // Broadcast, not shared: two enumerations each receive every change. A single-consumer pipe
        // would hand each change to whichever watcher happened to read first.
        [Fact]
        public async Task EveryWatcher_ReceivesEveryChange()
        {
            // Arrange
            var registry = new DeviceRegistry();
            await using var first = new ChangeRecorder(registry.Changes);
            await using var second = new ChangeRecorder(registry.Changes);

            // Act
            registry.AddIfAbsent(Create.Device("a"));

            // Assert
            await first.WaitForAsync(1);
            var firstChanges = first.Changes;
            await second.WaitForAsync(1);
            var secondChanges = second.Changes;

            Assert.Single(firstChanges);
            Assert.Single(secondChanges);
            Assert.Equal("a", firstChanges[0].Device.Id);
            Assert.Equal("a", secondChanges[0].Device.Id);
        }

        // GetAsyncEnumerator must subscribe eagerly. An `async` iterator body does not run until
        // the first MoveNextAsync, so if the subscribe lived inside one, every change published in
        // between would be dropped — precisely the window a consumer uses to read current state
        // before watching. NearbyDeviceCollection's constructor depends on this: it takes the
        // enumerator, then seeds from Devices, and must not lose a change arriving in between.
        [Fact]
        public async Task GetAsyncEnumerator_SubscribesBeforeTheFirstRead()
        {
            // Arrange
            var registry = new DeviceRegistry();
            var cts = new CancellationTokenSource();
            var enumerator = registry.Changes.GetAsyncEnumerator(cts.Token);

            // Act — published after subscribing, but before anything has read
            registry.AddIfAbsent(Create.Device("seed-window"));

            // Assert
            Assert.True(await enumerator.MoveNextAsync(), "The change must already be buffered.");
            Assert.Equal("seed-window", enumerator.Current.Device.Id);

            await cts.CancelAsync();
            await enumerator.DisposeAsync();
            cts.Dispose();
        }

        [Fact]
        public async Task ChangesBeforeSubscribing_AreNotReplayed()
        {
            // Arrange
            var registry = new DeviceRegistry();
            registry.AddIfAbsent(Create.Device("early"));

            await using var watch = new ChangeRecorder(registry.Changes);

            // Act
            registry.AddIfAbsent(Create.Device("late"));

            // Assert
            await watch.WaitForAsync(1);
            var changes = watch.Changes;
            Assert.Single(changes);
            Assert.Equal("late", changes[0].Device.Id);
        }

        // A watcher that ends its enumeration must stop costing anything. If unsubscribe were
        // skipped, the registry would write to a dead channel forever — the leak the removed events
        // made easy and this design is meant to make impossible.
        [Fact]
        public async Task EndingAnEnumeration_StopsDelivery()
        {
            // Arrange
            var registry = new DeviceRegistry();
            await using var watch = new ChangeRecorder(registry.Changes);
            registry.AddIfAbsent(Create.Device("a"));
            await watch.WaitForAsync(1);
            var before = watch.Changes;

            // Act
            registry.AddIfAbsent(Create.Device("b"));

            // A fixed wait, not a poll: this asserts that a change never reaches the earlier
            // snapshot, and polling can only establish that it has not reached it yet.
            await Task.Delay(50, TestContext.Current.CancellationToken);

            // Assert
            // Only the change published while watching should arrive.
            Assert.Single(before);
        }

        // A watcher that never reads must not block the publisher: each watcher buffers its own
        // changes in an unbounded channel.
        [Fact]
        public async Task AWatcherThatNeverReads_DoesNotBlockPublishing()
        {
            // Arrange
            var registry = new DeviceRegistry();
            using var cts = new CancellationTokenSource();
            var enumerator = registry.Changes.GetAsyncEnumerator(cts.Token);

            // Started and deliberately never awaited: the watcher has a live channel it is not
            // draining, which is the condition under test.
            var neverAwaited = enumerator.MoveNextAsync();

            // Act
            for (var i = 0; i < 100; i++)
            {
                registry.AddIfAbsent(Create.Device($"device-{i}"));
            }

            // Assert
            Assert.Equal(100, registry.Count);

            // Publishing hands the change to the channel, which completes the pending read on a
            // continuation — so this is reachable but not instantaneous. Asserting IsCompleted
            // synchronously races that continuation.
            await Wait.UntilAsync(() => neverAwaited.IsCompleted);
            Assert.True(neverAwaited.IsCompleted, "The first change should have completed the pending read.");
        }
    }
}
