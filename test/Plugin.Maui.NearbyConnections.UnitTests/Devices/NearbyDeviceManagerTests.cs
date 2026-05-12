namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Devices")]
public class NearbyDeviceManagerTests
{
    readonly NearbyDeviceManager _sut;

    public NearbyDeviceManagerTests()
    {
        _sut = new NearbyDeviceManager();
    }

    [TestClass]
    public sealed class RecordDeviceFound : NearbyDeviceManagerTests
    {
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
            // Arrange — manager is created empty in constructor

            // Act
            var devices = _sut.Devices;

            // Assert
            Assert.IsEmpty(devices);
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
            // Arrange — manager is created empty in constructor

            // Act
            _sut.Clear();

            // Assert
            Assert.IsEmpty(_sut.Devices);
        }
    }
}
