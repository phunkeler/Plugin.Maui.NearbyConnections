using System.Collections.Specialized;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Tests for <see cref="NearbyDeviceCollection"/>, the optional bindable projection over
/// <see cref="INearbyDevices.Changes"/>.
/// </summary>
[TestCategory("Devices")]
public class NearbyDeviceCollectionTests
{
    [TestClass]
    public sealed class Construction : NearbyDeviceCollectionTests
    {
        [TestMethod]
        public void NullSession_Throws()
            => Assert.ThrowsExactly<ArgumentNullException>(
                () => new NearbyDeviceCollection<NearbyDevice>(null!, _ => { }, static d => d));

        [TestMethod]
        public void NullMarshal_Throws()
            => Assert.ThrowsExactly<ArgumentNullException>(
                () => new NearbyDeviceCollection<NearbyDevice>(Create.Session(new FakeNearby()), null!, static d => d));

        // Constructing mid-session must show what is already there: the change stream carries
        // deltas, not history, so a collection built after discovery started would otherwise be
        // empty until the next change.
        [TestMethod]
        public async Task Seeds_FromDevicesAlreadyKnown()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));

            // Act
            using var devices = Create.Devices(session);

            // Assert
            Assert.HasCount(1, devices);
            Assert.AreEqual("a", devices[0].Id);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class Projection : NearbyDeviceCollectionTests
    {
        [TestMethod]
        public async Task DiscoveredDevice_IsAdded()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            using var devices = Create.Devices(session);

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Wait.UntilAsync(() => devices.Count == 1);

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
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            var device = Create.Device("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var devices = Create.Devices(session);

            var actions = new List<NotifyCollectionChangedAction>();
            devices.CollectionChanged += (_, e) => actions.Add(e.Action);

            platform.ConnectResult = Create.Connection(device: device);

            // Act
            await session.ConnectAsync(device, TestContext.CancellationToken);
            await Wait.UntilAsync(() => devices.Count == 1 && devices[0].Status is NearbyDeviceStatus.Connected);

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
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            var marshal = new InlineMarshal();
            using var devices = Create.Devices(session, marshal.Run);

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Wait.UntilAsync(() => devices.Count == 1);

            // Assert
            Assert.IsGreaterThan(0, marshal.Count, "Every mutation must run inside the caller's marshal callback.");
        }

        [TestMethod]
        public async Task Dispose_StopsTracking()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            var devices = Create.Devices(session);
            devices.Dispose();

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));

            // A fixed wait, not a poll: this asserts that something never arrives, and polling can
            // only establish that it has not arrived yet.
            await Task.Delay(50, TestContext.CancellationToken);

            // Assert
            Assert.IsEmpty(devices, "A disposed collection must stop applying changes.");
        }

        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            // Arrange
            var devices = Create.Devices(Create.Session(new FakeNearby()));

            // Act
            devices.Dispose();
            devices.Dispose();

            // Assert
            Assert.IsEmpty(devices);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class Filtering : NearbyDeviceCollectionTests
    {
        [TestMethod]
        public async Task NonMatchingDevice_IsNotShown()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            using var devices = Create.Devices(
                session, filter: static d => d.Status is NearbyDeviceStatus.Connected);

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Task.Delay(50, TestContext.CancellationToken);

            // Assert
            Assert.IsEmpty(devices, "A device that fails the filter must never enter the collection.");
        }

        // The filter is re-evaluated per change, so leaving the filtered set is a removal even
        // though the session reported an update.
        [TestMethod]
        public async Task DeviceLeavingTheFilteredSet_IsRemoved()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            var device = Create.Device("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var devices = Create.Devices(
                session, filter: static d => d.Status is NearbyDeviceStatus.Visible);

            platform.ConnectResult = Create.Connection(device: device);

            // Act
            await session.ConnectAsync(device, TestContext.CancellationToken);
            await Wait.UntilAsync(() => devices.Count == 0);

            // Assert
            Assert.IsEmpty(devices);
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class Projecting : NearbyDeviceCollectionTests
    {
        [TestMethod]
        public async Task Device_IsProjectedOntoARow()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            using var rows = new NearbyDeviceCollection<Row>(
                session, a => a(), project: static d => new Row(d));

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Wait.UntilAsync(() => rows.Count == 1);

            // Assert
            Assert.AreEqual("a", rows[0].Id);
        }

        // The whole reason the generic form takes an updater: a row that carries its own state must
        // survive its device's transitions rather than being rebuilt and losing it.
        [TestMethod]
        public async Task ChangedDevice_ReusesTheExistingRow()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            var device = Create.Device("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var rows = new NearbyDeviceCollection<Row>(
                session,
                a => a(),
                project: static d => new Row(d),
                update: static (row, d) => row.Update(d));

            var original = rows[0];
            platform.ConnectResult = Create.Connection(device: device);

            // Act
            await session.ConnectAsync(device, TestContext.CancellationToken);
            await Wait.UntilAsync(() => rows[0].Status is NearbyDeviceStatus.Connected);

            // Assert — connecting reports Connecting then Connected, so the one row took both.
            Assert.AreSame(original, rows[0], "A changed device must update its row, not replace it.");
            Assert.AreEqual(2, original.UpdateCount);
        }

        [TestMethod]
        public async Task WithoutAnUpdater_ChangedDeviceReplacesTheRow()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.CancellationToken);

            var device = Create.Device("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var rows = new NearbyDeviceCollection<Row>(
                session, a => a(), project: static d => new Row(d));

            var original = rows[0];
            platform.ConnectResult = Create.Connection(device: device);

            // Act
            await session.ConnectAsync(device, TestContext.CancellationToken);
            await Wait.UntilAsync(() => rows[0].Status is NearbyDeviceStatus.Connected);

            // Assert
            Assert.AreNotSame(original, rows[0]);
            Assert.HasCount(1, rows);
        }

        public TestContext TestContext { get; set; }
    }
}
