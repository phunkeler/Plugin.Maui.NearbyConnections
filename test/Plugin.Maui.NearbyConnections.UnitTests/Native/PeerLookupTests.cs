namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class PeerLookupTests
{
    readonly PeerLookup _sut;

    public PeerLookupTests()
    {
        _sut = new PeerLookup();
    }

    [TestClass]
    public sealed class Record : PeerLookupTests
    {
        [TestMethod]
        public void NewPeer_ReturnsDeviceWithKeyAndDisplayName()
        {
            // Arrange
            var key = "peer-1";
            var displayName = "Alice";

            // Act
            var device = _sut.Record(key, displayName);

            // Assert
            Assert.AreEqual(key, device.Id);
            Assert.AreEqual(displayName, device.DisplayName);
        }

        [TestMethod]
        public void ExistingPeer_ReturnsSameDeviceInstance()
        {
            // Arrange
            var key = "peer-1";
            var displayName = "Alice";
            var first = _sut.Record(key, displayName);

            // Act
            var second = _sut.Record(key, displayName);

            // Assert
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void ExistingPeer_DoesNotAdoptNewDisplayName()
        {
            // Arrange — a rediscovery re-records the same endpoint; the incumbent must survive.
            var key = "peer-1";
            var original = _sut.Record(key, "Alice");

            // Act
            var rediscovered = _sut.Record(key, "Alice (renamed)");

            // Assert
            Assert.AreSame(original, rediscovered);
            Assert.AreEqual("Alice", rediscovered.DisplayName);
        }
    }

    [TestClass]
    public sealed class Sanitize : PeerLookupTests
    {
        [TestMethod]
        public void PlainName_IsUnchanged()
        {
            // Arrange
            var displayName = "Alice's iPhone";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(displayName, device.DisplayName);
        }

        [TestMethod]
        public void NameWithNewlines_HasControlCharactersRemoved()
        {
            // Arrange — a forged log record appended to an attacker-chosen name.
            var displayName = "Alice\r\n[ERROR] transfer approved";
            var expected = "Alice[ERROR] transfer approved";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(expected, device.DisplayName);
        }

        [TestMethod]
        public void OverlongName_IsTruncatedToMaxLength()
        {
            // Arrange
            var displayName = new string('A', PeerLookup.MaxDisplayNameLength + 50);

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(PeerLookup.MaxDisplayNameLength, device.DisplayName!.Length);
        }

        [TestMethod]
        public void NameOfOnlyControlCharacters_BecomesNull()
        {
            // Arrange
            var displayName = "\r\n\t\u0000";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.IsNull(device.DisplayName);
        }

        [TestMethod]
        public void NullName_StaysNull()
        {
            // Arrange
            string? displayName = null;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.IsNull(device.DisplayName);
        }

        [TestMethod]
        public void TruncationDoesNotSplitSurrogatePair()
        {
            // Arrange — every rune is 2 UTF-16 units, so the cap lands exactly on a pair boundary
            // only if whole runes are appended.
            var displayName = string.Concat(Enumerable.Repeat("\U0001F600", PeerLookup.MaxDisplayNameLength));

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert — a split pair leaves a lone high surrogate at the end.
            Assert.IsFalse(char.IsHighSurrogate(device.DisplayName![^1]));
            Assert.AreEqual(PeerLookup.MaxDisplayNameLength, device.DisplayName.Length);
        }
    }

    [TestClass]
    public sealed class TryGetDevice : PeerLookupTests
    {
        [TestMethod]
        public void KnownKey_ReturnsTrueAndDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

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
    public sealed class Remove : PeerLookupTests
    {
        [TestMethod]
        public void KnownKey_ReturnsRemovedDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

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
            _sut.Record(key, "Alice");

            // Act
            _sut.Remove(key);

            // Assert
            Assert.IsFalse(_sut.TryGetDevice(key, out _));
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
    public sealed class Clear : PeerLookupTests
    {
        [TestMethod]
        public void RemovesAllTrackedPeers()
        {
            // Arrange
            _sut.Record("peer-1", "Alice");
            _sut.Record("peer-2", "Bob");

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
