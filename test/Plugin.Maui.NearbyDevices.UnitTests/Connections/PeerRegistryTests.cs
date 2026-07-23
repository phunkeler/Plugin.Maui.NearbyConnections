namespace Plugin.Maui.NearbyDevices.UnitTests;

[TestCategory("Connections")]
public class PeerRegistryTests
{
    readonly PeerRegistry<string> _sut;

    public PeerRegistryTests()
    {
        _sut = new PeerRegistry<string>();
    }

    [TestClass]
    public sealed class Record : PeerRegistryTests
    {
        [TestMethod]
        public void NewPeer_ReturnsDeviceWithKeyAndDisplayName()
        {
            // Arrange
            var key = "peer-1";
            var handle = "native-handle-1";
            var displayName = "Alice";

            // Act
            var device = _sut.Record(key, handle, displayName);

            // Assert
            Assert.AreEqual(key, device.Id);
            Assert.AreEqual(displayName, device.DisplayName);
        }

        [TestMethod]
        public void ExistingPeer_ReturnsSameDeviceInstance()
        {
            // Arrange
            var key = "peer-1";
            var handle = "native-handle-1";
            var displayName = "Alice";
            var first = _sut.Record(key, handle, displayName);

            // Act
            var second = _sut.Record(key, handle, displayName);

            // Assert
            Assert.AreSame(first, second);
        }
    }

    [TestClass]
    public sealed class TryGetHandle : PeerRegistryTests
    {
        [TestMethod]
        public void KnownKey_ReturnsTrueAndHandle()
        {
            // Arrange
            var key = "peer-1";
            var handle = "native-handle-1";
            _sut.Record(key, handle, "Alice");

            // Act
            var found = _sut.TryGetHandle(key, out var resolvedHandle);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(handle, resolvedHandle);
        }

        [TestMethod]
        public void UnknownKey_ReturnsFalseAndNullOut()
        {
            // Arrange
            var key = "peer-unknown";

            // Act
            var found = _sut.TryGetHandle(key, out var resolvedHandle);

            // Assert
            Assert.IsFalse(found);
            Assert.IsNull(resolvedHandle);
        }
    }

    [TestClass]
    public sealed class TryGetDevice : PeerRegistryTests
    {
        [TestMethod]
        public void KnownKey_ReturnsTrueAndDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "native-handle-1", "Alice");

            // Act
            var found = _sut.TryGetDevice(key, out var device);

            // Assert
            Assert.IsTrue(found);
            Assert.IsNotNull(device);
            Assert.AreEqual(key, device.Id);
        }

        [TestMethod]
        public void UnknownKey_ReturnsFalseAndNullOut()
        {
            // Arrange
            var key = "peer-unknown";

            // Act
            var found = _sut.TryGetDevice(key, out var device);

            // Assert
            Assert.IsFalse(found);
            Assert.IsNull(device);
        }
    }

    [TestClass]
    public sealed class Remove : PeerRegistryTests
    {
        [TestMethod]
        public void KnownKey_ReturnsRemovedDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "native-handle-1", "Alice");

            // Act
            var removed = _sut.Remove(key);

            // Assert
            Assert.IsNotNull(removed);
            Assert.AreEqual(key, removed.Id);
        }

        [TestMethod]
        public void KnownKey_IsNoLongerResolvable()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "native-handle-1", "Alice");

            // Act
            _sut.Remove(key);

            // Assert
            Assert.IsFalse(_sut.TryGetDevice(key, out _));
            Assert.IsFalse(_sut.TryGetHandle(key, out _));
        }

        [TestMethod]
        public void UnknownKey_ReturnsNull()
        {
            // Arrange
            var key = "peer-unknown";

            // Act
            var removed = _sut.Remove(key);

            // Assert
            Assert.IsNull(removed);
        }
    }

    [TestClass]
    public sealed class Clear : PeerRegistryTests
    {
        [TestMethod]
        public void RemovesAllTrackedPeers()
        {
            // Arrange
            _sut.Record("peer-1", "native-handle-1", "Alice");
            _sut.Record("peer-2", "native-handle-2", "Bob");

            // Act
            _sut.Clear();

            // Assert
            Assert.IsFalse(_sut.TryGetDevice("peer-1", out _));
            Assert.IsFalse(_sut.TryGetDevice("peer-2", out _));
        }

        [TestMethod]
        public void OnEmptyRegistry_DoesNotThrow()
        {
            // Arrange — registry is created empty in constructor

            // Act
            _sut.Clear();

            // Assert
            Assert.IsFalse(_sut.TryGetDevice("peer-1", out _));
        }
    }
}
