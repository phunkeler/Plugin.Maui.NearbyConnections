using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

public class NearbyDeviceManagerTests
{
    readonly FakeTimeProvider _timeProvider;
    readonly NearbyConnectionsEvents _events;
    readonly NearbyDeviceManager _sut;

    public NearbyDeviceManagerTests()
    {
        _timeProvider = new();
        _events = new();
        _sut = new NearbyDeviceManager(_timeProvider, _events);
    }

    [TestClass]
    public sealed class RecordDeviceFound : NearbyDeviceManagerTests
    {
        [TestMethod]
        public void NewDevice_ReturnsDeviceWithDiscoveredState()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";

            // Act
            var device = _sut.RecordDeviceFound(id, displayName);

            // Assert
            Assert.AreEqual(NearbyDeviceState.Discovered, device.State);
        }

        [TestMethod]
        public void NewDevice_SetsLastSeenToCurrentTime()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            var expectedTime = _timeProvider.GetUtcNow();

            // Act
            var device = _sut.RecordDeviceFound(id, displayName);

            // Assert
            Assert.AreEqual(expectedTime, device.LastSeen);
        }

        [TestMethod]
        public void NewDevice_IsAddedToDevices()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";

            // Act
            _sut.RecordDeviceFound(id, displayName);

            // Assert
            Assert.HasCount(1, _sut.Devices);
        }

        [TestMethod]
        public void ExistingDevice_UpdatesLastSeen()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
            var expectedTime = _timeProvider.GetUtcNow();

            // Act
            var device = _sut.RecordDeviceFound(id, displayName);

            // Assert
            Assert.AreEqual(expectedTime, device.LastSeen);
        }

        [TestMethod]
        public void ExistingDevice_DoesNotAddDuplicate()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);

            // Act
            _sut.RecordDeviceFound(id, displayName);

            // Assert
            Assert.HasCount(1, _sut.Devices);
        }

        [TestMethod]
        public void ExistingDevice_ReturnsSameInstance()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            var first = _sut.RecordDeviceFound(id, displayName);

            // Act
            var second = _sut.RecordDeviceFound(id, displayName);

            // Assert
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void ExistingDevice_DoesNotResetStateToDiscovered()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            var device = _sut.RecordDeviceFound(id, displayName);
            _sut.SetState(id, NearbyDeviceState.Connected);

            // Act
            _sut.RecordDeviceFound(id, displayName);

            // Assert
            Assert.AreEqual(NearbyDeviceState.Connected, device.State);
        }
    }

    [TestClass]
    public sealed class GetOrAddDevice : NearbyDeviceManagerTests
    {
        [TestMethod]
        public void NewDevice_ReturnsDeviceWithGivenInitialState()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Bob";
            var initialState = NearbyDeviceState.ConnectionRequestedInbound;

            // Act
            var device = _sut.GetOrAddDevice(id, displayName, initialState);

            // Assert
            Assert.AreEqual(initialState, device.State);
        }

        [TestMethod]
        public void NewDevice_IsAddedToDevices()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Bob";

            // Act
            _sut.GetOrAddDevice(id, displayName, NearbyDeviceState.Connected);

            // Assert
            Assert.HasCount(1, _sut.Devices);
        }

        [TestMethod]
        public void ExistingDevice_ReturnsExistingInstance()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Bob";
            var first = _sut.GetOrAddDevice(id, displayName, NearbyDeviceState.Discovered);

            // Act
            var second = _sut.GetOrAddDevice(id, displayName, NearbyDeviceState.Connected);

            // Assert
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void ExistingDevice_DoesNotOverwriteState()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Bob";
            var originalState = NearbyDeviceState.Discovered;
            _sut.GetOrAddDevice(id, displayName, originalState);

            // Act
            var returned = _sut.GetOrAddDevice(id, displayName, NearbyDeviceState.Connected);

            // Assert
            Assert.AreEqual(originalState, returned.State);
        }

        [TestMethod]
        public void ExistingDevice_DoesNotAddDuplicate()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Bob";
            _sut.GetOrAddDevice(id, displayName, NearbyDeviceState.Discovered);

            // Act
            _sut.GetOrAddDevice(id, displayName, NearbyDeviceState.Connected);

            // Assert
            Assert.HasCount(1, _sut.Devices);
        }
    }

    [TestClass]
    public sealed class RemoveDevice : NearbyDeviceManagerTests
    {
        [TestMethod]
        public void KnownDevice_ReturnsRemovedDevice()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);

            // Act
            var removed = _sut.RemoveDevice(id);

            // Assert
            Assert.IsNotNull(removed);
            Assert.AreEqual(id, removed.Id);
        }

        [TestMethod]
        public void KnownDevice_IsRemovedFromDevices()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);

            // Act
            _sut.RemoveDevice(id);

            // Assert
            Assert.IsEmpty(_sut.Devices);
        }

        [TestMethod]
        public void UnknownDevice_ReturnsNull()
        {
            // Arrange
            var id = "peer-unknown";

            // Act
            var removed = _sut.RemoveDevice(id);

            // Assert
            Assert.IsNull(removed);
        }
    }

    [TestClass]
    public sealed class SetState : NearbyDeviceManagerTests
    {
        [TestMethod]
        public void KnownDevice_UpdatesState()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);

            // Act
            _sut.SetState(id, NearbyDeviceState.Connected);

            // Assert
            Assert.IsTrue(_sut.TryGetDevice(id, out var device));
            Assert.AreEqual(NearbyDeviceState.Connected, device.State);
        }

        [TestMethod]
        public void KnownDevice_ReturnsDevice()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);

            // Act
            var result = _sut.SetState(id, NearbyDeviceState.Connected);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(id, result.Id);
        }

        [TestMethod]
        public void UnknownDevice_ReturnsNull()
        {
            // Arrange
            var id = "peer-unknown";

            // Act
            var result = _sut.SetState(id, NearbyDeviceState.Connected);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void StateChange_RaisesDeviceStateChangedEvent()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);
            NearbyDeviceStateChangedEventArgs? captured = null;
            _events.DeviceStateChanged += (_, args) => captured = args;

            // Act
            _sut.SetState(id, NearbyDeviceState.Connected);

            // Assert
            Assert.IsNotNull(captured);
        }

        [TestMethod]
        public void StateChange_EventCarriesPreviousState()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);
            NearbyDeviceStateChangedEventArgs? captured = null;
            _events.DeviceStateChanged += (_, args) => captured = args;

            // Act
            _sut.SetState(id, NearbyDeviceState.Connected);

            // Assert
            Assert.AreEqual(NearbyDeviceState.Discovered, captured!.PreviousState);
        }

        [TestMethod]
        public void StateChange_EventCarriesNewState()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            var newState = NearbyDeviceState.Connected;
            _sut.RecordDeviceFound(id, displayName);
            NearbyDeviceStateChangedEventArgs? captured = null;
            _events.DeviceStateChanged += (_, args) => captured = args;

            // Act
            _sut.SetState(id, newState);

            // Assert
            Assert.AreEqual(newState, captured!.NearbyDevice.State);
        }

        [TestMethod]
        public void NoStateChange_DoesNotRaiseDeviceStateChangedEvent()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);
            var raised = false;
            _events.DeviceStateChanged += (_, _) => raised = true;

            // Act
            _sut.SetState(id, NearbyDeviceState.Discovered);

            // Assert
            Assert.IsFalse(raised);
        }

        [TestMethod]
        public void TransitionToDiscovered_UpdatesLastSeen()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);
            _sut.SetState(id, NearbyDeviceState.Connected);
            _timeProvider.Advance(TimeSpan.FromSeconds(5));
            var expectedTime = _timeProvider.GetUtcNow();

            // Act
            _sut.SetState(id, NearbyDeviceState.Discovered);

            // Assert
            Assert.IsTrue(_sut.TryGetDevice(id, out var device));
            Assert.AreEqual(expectedTime, device.LastSeen);
        }

        [TestMethod]
        public void TransitionToNonDiscoveredState_DoesNotUpdateLastSeen()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            var device = _sut.RecordDeviceFound(id, displayName);
            var lastSeenAfterFound = device.LastSeen;
            _timeProvider.Advance(TimeSpan.FromSeconds(5));

            // Act
            _sut.SetState(id, NearbyDeviceState.Connected);

            // Assert
            Assert.AreEqual(lastSeenAfterFound, device.LastSeen);
        }
    }

    [TestClass]
    public sealed class TryGetDevice : NearbyDeviceManagerTests
    {
        [TestMethod]
        public void KnownDevice_ReturnsTrueAndDevice()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);

            // Act
            var found = _sut.TryGetDevice(id, out var device);

            // Assert
            Assert.IsTrue(found);
            Assert.IsNotNull(device);
            Assert.AreEqual(id, device.Id);
        }

        [TestMethod]
        public void UnknownDevice_ReturnsFalseAndNullOut()
        {
            // Arrange
            var id = "peer-unknown";

            // Act
            var found = _sut.TryGetDevice(id, out var device);

            // Assert
            Assert.IsFalse(found);
            Assert.IsNull(device);
        }
    }

    [TestClass]
    public sealed class DevicesSnapshot : NearbyDeviceManagerTests
    {
        [TestMethod]
        public void EmptyManager_ReturnsEmptyList()
        {
            // Act & Assert
            Assert.IsEmpty(_sut.Devices);
        }

        [TestMethod]
        public void SnapshotIsIsolatedFromSubsequentChanges()
        {
            // Arrange
            var firstId = "peer-1";
            var firstDisplayName = "Alice";
            var secondId = "peer-2";
            var secondDisplayName = "Bob";
            _sut.RecordDeviceFound(firstId, firstDisplayName);
            var snapshot = _sut.Devices;

            // Act
            _sut.RecordDeviceFound(secondId, secondDisplayName);

            // Assert
            Assert.HasCount(1, snapshot);
        }
    }

    [TestClass]
    public sealed class Clear : NearbyDeviceManagerTests
    {
        [TestMethod]
        public void RemovesAllTrackedDevices()
        {
            // Arrange
            _sut.RecordDeviceFound("peer-1", "Alice");
            _sut.RecordDeviceFound("peer-2", "Bob");

            // Act
            _sut.Clear();

            // Assert
            Assert.IsEmpty(_sut.Devices);
        }

        [TestMethod]
        public void OnEmptyManager_DoesNotThrow()
        {
            // Act
            _sut.Clear();

            // Assert
            Assert.IsEmpty(_sut.Devices);
        }

        [TestMethod]
        public void DoesNotRaiseDeviceStateChangedEvent()
        {
            // Arrange
            var id = "peer-1";
            var displayName = "Alice";
            _sut.RecordDeviceFound(id, displayName);
            var raised = false;
            _events.DeviceStateChanged += (_, _) => raised = true;

            // Act
            _sut.Clear();

            // Assert
            Assert.IsFalse(raised);
        }
    }
}