using System.Collections.Specialized;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Tests for <see cref="NearbyDeviceCollection"/>, the optional bindable projection over
/// <see cref="INearbyDevices.Changes"/>.
/// </summary>
[Trait("Category", "Devices")]
public class NearbyDeviceCollectionTests
{
    public sealed class Construction : NearbyDeviceCollectionTests
    {
        [Fact]
        public void NullSession_Throws()
            => Assert.Throws<ArgumentNullException>(
                () => new NearbyDeviceCollection<NearbyDevice>(null!, _ => { }, static d => d));

        [Fact]
        public void NullMarshal_Throws()
            => Assert.Throws<ArgumentNullException>(
                () => new NearbyDeviceCollection<NearbyDevice>(Create.Session(new FakeNearby()), null!, static d => d));

        // Constructing mid-session must show what is already there: the change stream carries
        // deltas, not history, so a collection built after discovery started would otherwise be
        // empty until the next change.
        [Fact]
        public async Task Seeds_FromDevicesAlreadyKnown()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));

            // Act
            using var devices = Create.Devices(session);

            // Assert
            Assert.Single(devices);
            Assert.Equal("a", devices[0].Id);
        }
    }

    public sealed class Projection : NearbyDeviceCollectionTests
    {
        [Fact]
        public async Task DiscoveredDevice_IsAdded()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            using var devices = Create.Devices(session);

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Wait.UntilAsync(() => devices.Count == 1);

            // Assert
            Assert.Single(devices);
        }

        // A device is a value, so an update is a replacement. The indexer assignment raises Replace,
        // which is what lets a bound row update in place instead of moving.
        [Fact]
        public async Task StatusChange_ReplacesInPlace_AndRaisesReplace()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = Create.Device("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var devices = Create.Devices(session);

            var actions = new List<NotifyCollectionChangedAction>();
            devices.CollectionChanged += (_, e) => actions.Add(e.Action);

            platform.ConnectResult = Create.Connection(device: device);

            // Act
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => devices.Count == 1 && devices[0].Status is NearbyDeviceStatus.Connected);

            // Assert
            Assert.Single(devices);
            Assert.Equal(NearbyDeviceStatus.Connected, devices[0].Status);
            Assert.Contains(NotifyCollectionChangedAction.Replace, actions);
            Assert.DoesNotContain(NotifyCollectionChangedAction.Remove, actions);
        }

        [Fact]
        public async Task Mutations_GoThroughTheMarshalCallback()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var marshal = new InlineMarshal();
            using var devices = Create.Devices(session, marshal.Run);

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Wait.UntilAsync(() => devices.Count == 1);

            // Assert
            Assert.True(marshal.Count > 0, "Every mutation must run inside the caller's marshal callback.");
        }

        [Fact]
        public async Task Dispose_StopsTracking()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var devices = Create.Devices(session);
            devices.Dispose();

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));

            // A fixed wait, not a poll: this asserts that something never arrives, and polling can
            // only establish that it has not arrived yet.
            await Task.Delay(50, TestContext.Current.CancellationToken);

            // Assert
            // A disposed collection must stop applying changes.
            Assert.Empty(devices);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            // Arrange
            var devices = Create.Devices(Create.Session(new FakeNearby()));

            // Act
            devices.Dispose();
            devices.Dispose();

            // Assert
            Assert.Empty(devices);
        }
    }

    public sealed class Filtering : NearbyDeviceCollectionTests
    {
        [Fact]
        public async Task NonMatchingDevice_IsNotShown()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            using var devices = Create.Devices(
                session, filter: static d => d.Status is NearbyDeviceStatus.Connected);

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Task.Delay(50, TestContext.Current.CancellationToken);

            // Assert
            // A device that fails the filter must never enter the collection.
            Assert.Empty(devices);
        }

        // The filter is re-evaluated per change, so leaving the filtered set is a removal even
        // though the session reported an update.
        [Fact]
        public async Task DeviceLeavingTheFilteredSet_IsRemoved()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = Create.Device("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var devices = Create.Devices(
                session, filter: static d => d.Status is NearbyDeviceStatus.Visible);

            platform.ConnectResult = Create.Connection(device: device);

            // Act
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => devices.Count == 0);

            // Assert
            Assert.Empty(devices);
        }
    }

    public sealed class Projecting : NearbyDeviceCollectionTests
    {
        [Fact]
        public async Task Device_IsProjectedOntoARow()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            using var rows = new NearbyDeviceCollection<Row>(
                session, a => a(), project: static d => new Row(d));

            // Act
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));
            await Wait.UntilAsync(() => rows.Count == 1);

            // Assert
            Assert.Equal("a", rows[0].Id);
        }

        // The whole reason the generic form takes an updater: a row that carries its own state must
        // survive its device's transitions rather than being rebuilt and losing it.
        [Fact]
        public async Task ChangedDevice_ReusesTheExistingRow()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

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
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => rows[0].Status is NearbyDeviceStatus.Connected);

            // Assert — connecting reports Connecting then Connected, so the one row took both.
            // A changed device must update its row, not replace it.
            Assert.Same(original, rows[0]);
            Assert.Equal(2, original.UpdateCount);
        }

        [Fact]
        public async Task WithoutAnUpdater_ChangedDeviceReplacesTheRow()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            var device = Create.Device("a", "Alice");
            await platform.EmitDeviceFoundAsync(device);

            using var rows = new NearbyDeviceCollection<Row>(
                session, a => a(), project: static d => new Row(d));

            var original = rows[0];
            platform.ConnectResult = Create.Connection(device: device);

            // Act
            await session.ConnectAsync(device, TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => rows[0].Status is NearbyDeviceStatus.Connected);

            // Assert
            Assert.NotSame(original, rows[0]);
            Assert.Single(rows);
        }
    }

    public sealed class StreamFaults : NearbyDeviceCollectionTests
    {
        // Nothing awaits the watch loop, so a swallowed fault leaves a bound view frozen with no
        // diagnostic anywhere. The fault is rethrown through marshal instead.
        [Fact]
        public async Task FaultedStream_RethrowsThroughMarshal()
        {
            // Arrange
            var session = new FaultingDevices();
            Exception? observed = null;

            // Act
            using var devices = Create.Devices(
                session,
                marshal: action =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        observed = ex;
                    }
                });

            await Wait.UntilAsync(() => observed is not null);

            // Assert
            var nearbyException = Assert.IsAssignableFrom<NearbyException>(observed);
            Assert.Same(session.Fault, nearbyException.InnerException);
        }

        // Disposal is the one cancellation that is a normal teardown. It must not reach the
        // rethrow path the way any other fault does.
        [Fact]
        public async Task Disposal_DoesNotRethrow()
        {
            // Arrange
            var platform = new FakeNearby();
            var session = Create.Session(platform);
            await session.StartDiscoveryAsync(TestContext.Current.CancellationToken);

            Exception? observed = null;
            var devices = Create.Devices(
                session,
                marshal: action =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        observed = ex;
                    }
                });

            // Act
            devices.Dispose();
            await platform.EmitDeviceFoundAsync(Create.Device("a", "Alice"));

            // Assert
            Assert.Null(observed);
        }
    }
}
