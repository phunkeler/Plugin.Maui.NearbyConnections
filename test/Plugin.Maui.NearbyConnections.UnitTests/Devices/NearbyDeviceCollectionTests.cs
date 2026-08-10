using System.Collections.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Tests for <see cref="NearbyDeviceCollection"/>, the optional bindable projection over
/// <see cref="INearbyDevices.Changes"/>.
/// </summary>
[TestCategory("Devices")]
public class NearbyDeviceCollectionTests
{
    /// <summary>
    /// Runs marshalled actions inline and counts them, so a test can assert that mutation went
    /// through the callback rather than around it.
    /// </summary>
    sealed class InlineMarshal
    {
        public int Count { get; private set; }

        public void Run(Action action)
        {
            Count++;
            action();
        }
    }

    static NearbyImplementation CreateSession(FakeNearby platform)
        => new(platform, new NearbyOptions(), NullLogger.Instance);

    static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [TestClass]
    public sealed class Construction : NearbyDeviceCollectionTests
    {
        [TestMethod]
        public void NullSession_Throws()
            => Assert.ThrowsExactly<ArgumentNullException>(
                () => new NearbyDeviceCollection(null!, _ => { }));

        [TestMethod]
        public void NullMarshal_Throws()
            => Assert.ThrowsExactly<ArgumentNullException>(
                () => new NearbyDeviceCollection(CreateSession(new FakeNearby()), null!));

        [TestMethod]
        public void NegativeStaleAfter_Throws()
            => Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new NearbyDeviceCollection(
                    CreateSession(new FakeNearby()),
                    _ => { },
                    TimeSpan.FromSeconds(-1)));

        // Constructing mid-session must show what is already there: the change stream carries
        // deltas, not history, so a collection built after discovery started would otherwise be
        // empty until the next change.
        [TestMethod]
        public async Task Seeds_FromDevicesAlreadyKnown()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();
            await platform.EmitDeviceFoundAsync(new NearbyDevice("a", "Alice"));

            // Act
            using var devices = new NearbyDeviceCollection(session, a => a());

            // Assert
            Assert.HasCount(1, devices);
            Assert.AreEqual("a", devices[0].Id);
        }
    }

    [TestClass]
    public sealed class Projection : NearbyDeviceCollectionTests
    {
        [TestMethod]
        public async Task DiscoveredDevice_IsAdded()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();

            using var devices = new NearbyDeviceCollection(session, a => a());

            // Act
            await platform.EmitDeviceFoundAsync(new NearbyDevice("a", "Alice"));
            await WaitForAsync(() => devices.Count == 1);

            // Assert
            Assert.HasCount(1, devices);
        }

        // A device is a value, so an update is a replacement. The indexer assignment raises Replace,
        // which is what lets a bound row update in place instead of moving.
        [TestMethod]
        public async Task StatusChange_ReplacesInPlace_AndRaisesReplace()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var devices = new NearbyDeviceCollection(session, a => a());

            var actions = new List<NotifyCollectionChangedAction>();
            devices.CollectionChanged += (_, e) => actions.Add(e.Action);

            platform.ConnectResult = new NearbyConnection(
                device,
                System.Threading.Channels.Channel.CreateUnbounded<NearbyPayload>(),
                (_, _) => ValueTask.CompletedTask,
                (_, _, _) => Task.CompletedTask,
                () => ValueTask.CompletedTask);

            // Act
            await session.ConnectAsync(device);
            await WaitForAsync(() => devices.Count == 1 && devices[0].Status is NearbyDeviceStatus.Connected);

            // Assert
            Assert.HasCount(1, devices);
            Assert.AreEqual(NearbyDeviceStatus.Connected, devices[0].Status);
            Assert.Contains(NotifyCollectionChangedAction.Replace, actions);
            Assert.DoesNotContain(NotifyCollectionChangedAction.Remove, actions);
        }

        [TestMethod]
        public async Task Mutations_GoThroughTheMarshalCallback()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();

            var marshal = new InlineMarshal();
            using var devices = new NearbyDeviceCollection(session, marshal.Run);

            // Act
            await platform.EmitDeviceFoundAsync(new NearbyDevice("a", "Alice"));
            await WaitForAsync(() => devices.Count == 1);

            // Assert
            Assert.IsGreaterThan(0, marshal.Count, "Every mutation must run inside the caller's marshal callback.");
        }

        [TestMethod]
        public async Task Dispose_StopsTracking()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();

            var devices = new NearbyDeviceCollection(session, a => a());
            devices.Dispose();

            // Act
            await platform.EmitDeviceFoundAsync(new NearbyDevice("a", "Alice"));
            await Task.Delay(50);

            // Assert
            Assert.IsEmpty(devices, "A disposed collection must stop applying changes.");
        }

        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            // Arrange
            var devices = new NearbyDeviceCollection(CreateSession(new FakeNearby()), a => a());

            // Act
            devices.Dispose();
            devices.Dispose();

            // Assert
            Assert.IsEmpty(devices);
        }
    }

    [TestClass]
    public sealed class StaleEviction : NearbyDeviceCollectionTests
    {
        // Neither platform reliably reports every departure, so a device carried out of range can
        // simply stop being seen. Without the sweep it would linger forever.
        [TestMethod]
        public async Task UnseenVisibleDevice_IsEvicted()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();
            await platform.EmitDeviceFoundAsync(new NearbyDevice("a", "Alice"));

            var time = new FakeTimeProvider();
            using var devices = new NearbyDeviceCollection(
                session,
                a => a(),
                TimeSpan.FromSeconds(30),
                time);

            Assert.HasCount(1, devices);

            // Act
            time.Advance(TimeSpan.FromSeconds(31));
            await WaitForAsync(() => devices.Count == 0);

            // Assert
            Assert.IsEmpty(devices);
        }

        // A connected device is demonstrably still there, whatever discovery has stopped reporting.
        // Evicting it would delete a live conversation from the UI.
        [TestMethod]
        public async Task ConnectedDevice_IsNeverEvicted()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();

            var device = new NearbyDevice("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            platform.ConnectResult = new NearbyConnection(
                device,
                System.Threading.Channels.Channel.CreateUnbounded<NearbyPayload>(),
                (_, _) => ValueTask.CompletedTask,
                (_, _, _) => Task.CompletedTask,
                () => ValueTask.CompletedTask);

            await session.ConnectAsync(device);

            var time = new FakeTimeProvider();
            using var devices = new NearbyDeviceCollection(
                session,
                a => a(),
                TimeSpan.FromSeconds(30),
                time);

            // Act
            time.Advance(TimeSpan.FromMinutes(10));
            await Task.Delay(50);

            // Assert
            Assert.HasCount(1, devices);
            Assert.AreEqual(NearbyDeviceStatus.Connected, devices[0].Status);
        }

        [TestMethod]
        public async Task NullStaleAfter_DisablesEviction()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = CreateSession(platform);
            await session.StartDiscoveryAsync();
            await platform.EmitDeviceFoundAsync(new NearbyDevice("a", "Alice"));

            var time = new FakeTimeProvider();
            using var devices = new NearbyDeviceCollection(session, a => a(), staleAfter: null, time);

            // Act
            time.Advance(TimeSpan.FromHours(1));
            await Task.Delay(50);

            // Assert
            Assert.HasCount(1, devices);
        }
    }
}
