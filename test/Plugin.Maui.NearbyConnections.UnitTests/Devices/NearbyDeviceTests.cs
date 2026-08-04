namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Devices")]
public class NearbyDeviceTests
{
    [TestClass]
    public sealed class EqualsMethod : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals(b);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SameId_DifferentDisplayName_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Bob");

            // Act
            var result = a.Equals(b);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a.Equals(b);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void SameReference_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals(a);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Null_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals(null);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void NonDeviceObject_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals((object)"ep1");

            // Assert
            Assert.IsFalse(result);
        }
    }

    [TestClass]
    public sealed class EqualityOperator : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a == b;

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a == b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void LeftNull_ReturnsFalse()
        {
            // Arrange
            NearbyDevice? a = null;
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a == b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RightNull_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            NearbyDevice? b = null;

            // Act
            var result = a == b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void BothNull_ReturnsTrue()
        {
            // Arrange
            NearbyDevice? a = null;
            NearbyDevice? b = null;

            // Act
            var result = a == b;

            // Assert
            Assert.IsTrue(result);
        }
    }

    [TestClass]
    public sealed class InequalityOperator : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a != b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a != b;

            // Assert
            Assert.IsTrue(result);
        }
    }

    [TestClass]
    public sealed class HashCode : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsSameHashCode()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Bob");

            // Act
            var result = a.GetHashCode() == b.GetHashCode();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsDifferentHashCode()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a.GetHashCode() == b.GetHashCode();

            // Assert
            Assert.IsFalse(result);
        }
    }

    [TestClass]
    public sealed class Identity : NearbyDeviceTests
    {
        // The load-bearing guarantee of the record -> observable class change. PeerRegistry and
        // _activeConnections key on NearbyDevice, so if identity shifted as a device moved through
        // its lifecycle, every existing dictionary entry would be stranded — a device would connect
        // and then be unreachable by lookup.
        [TestMethod]
        public void HashCodeAndEquality_AreStable_AcrossStateTransitions()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var sameId = new NearbyDevice("ep1", "Alice");
            var originalHash = device.GetHashCode();

            var dictionary = new Dictionary<NearbyDevice, string> { [device] = "tracked" };

            // Act — walk the full lifecycle
            device.Status = NearbyDeviceStatus.RequestReceived;
            device.Role = ConnectionRole.Acceptor;
            device.Status = NearbyDeviceStatus.Connecting;
            device.Status = NearbyDeviceStatus.Connected;
            device.DisplayName = "Alice (renamed)";

            // Assert — identity never moved, so the entry is still reachable
            Assert.AreEqual(originalHash, device.GetHashCode());
            Assert.IsTrue(device.Equals(sameId));
            Assert.IsTrue(device == sameId);
            Assert.IsTrue(dictionary.TryGetValue(device, out var tracked));
            Assert.AreEqual("tracked", tracked);
            Assert.IsTrue(dictionary.ContainsKey(sameId));
        }

        [TestMethod]
        public void Constructor_NullId_Throws()
            => Assert.ThrowsExactly<ArgumentNullException>(() => new NearbyDevice(null!, "Alice"));

        [TestMethod]
        public void NewDevice_StartsVisible_WithNoRoleOrConnection()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
            Assert.IsNull(device.Role);
            Assert.IsNull(device.Connection);
        }
    }

    [TestClass]
    public sealed class PropertyChangedNotification : NearbyDeviceTests
    {
        [TestMethod]
        public void Status_RaisesPropertyChanged()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var raised = new List<string?>();
            device.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            // Act
            device.Status = NearbyDeviceStatus.Connected;

            // Assert
            Assert.Contains(nameof(NearbyDevice.Status), raised);
        }

        [TestMethod]
        public void Role_And_DisplayName_RaisePropertyChanged()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var raised = new List<string?>();
            device.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            // Act
            device.Role = ConnectionRole.Initiator;
            device.DisplayName = "Bob";

            // Assert
            Assert.Contains(nameof(NearbyDevice.Role), raised);
            Assert.Contains(nameof(NearbyDevice.DisplayName), raised);
        }

        // Suppressing no-op sets keeps bindings from re-rendering on every redundant platform
        // callback — the platforms re-report unchanged state routinely.
        [TestMethod]
        public void SettingSameValue_DoesNotRaisePropertyChanged()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice") { Status = NearbyDeviceStatus.Connected };
            var raised = new List<string?>();
            device.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            // Act
            device.Status = NearbyDeviceStatus.Connected;
            device.DisplayName = "Alice";

            // Assert
            Assert.IsEmpty(raised);
        }
    }
}
