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
        // The load-bearing guarantee. Id-only equality replaces the record's generated member-wise
        // equality, so a device that merely changed status stays the same device: registries and
        // _activeConnections key on id, and an identity that shifted mid-lifecycle would strand
        // every existing entry. Generated equality would break every assertion below.
        [TestMethod]
        public void HashCodeAndEquality_AreStable_AcrossStateTransitions()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var sameId = new NearbyDevice("ep1", "Alice");
            var originalHash = device.GetHashCode();

            var dictionary = new Dictionary<NearbyDevice, string> { [device] = "tracked" };

            // Act — walk the full lifecycle, which for a value means producing new snapshots
            var connected = device
                with { Status = NearbyDeviceStatus.RequestReceived }
                with { Status = NearbyDeviceStatus.Connecting, Role = ConnectionRole.Acceptor }
                with { Status = NearbyDeviceStatus.Connected }
                with { DisplayName = "Alice (renamed)" };

            // Assert — identity never moved, so the entry is still reachable
            Assert.AreEqual(originalHash, connected.GetHashCode());
            Assert.IsTrue(connected.Equals(sameId));
            Assert.IsTrue(connected == sameId);
            Assert.IsTrue(dictionary.TryGetValue(connected, out var tracked));
            Assert.AreEqual("tracked", tracked);
            Assert.IsTrue(dictionary.ContainsKey(sameId));
        }

        // `with` must not be a way to forge a different device: Id has no init accessor, so this
        // is enforced by the compiler. This test documents the intent that keeps it that way.
        [TestMethod]
        public void With_PreservesIdentity()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");

            // Act
            var updated = device with { Status = NearbyDeviceStatus.Connected };

            // Assert
            Assert.AreEqual("ep1", updated.Id);
            Assert.AreEqual(device, updated);
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status, "The original snapshot must not change.");
            Assert.AreEqual(NearbyDeviceStatus.Connected, updated.Status);
        }

        [TestMethod]
        public void Constructor_NullId_Throws()
            => Assert.ThrowsExactly<ArgumentNullException>(() => new NearbyDevice(null!, "Alice"));

        [TestMethod]
        public void NewDevice_StartsVisible()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
            Assert.IsNull(device.Role, "A device that has only been discovered plays no role.");
        }
    }

    [TestClass]
    public sealed class Snapshots : NearbyDeviceTests
    {
        // A device is a value handed out by the session; nothing may write to it afterwards. If any
        // of these properties ever regained a public setter, a consumer could mutate a snapshot the
        // session still holds, and the thread-safety the value type exists to provide would be gone.
        [TestMethod]
        public void MutableProperties_AreInitOnly()
        {
            // Arrange
            var properties = new[]
            {
                nameof(NearbyDevice.Id),
                nameof(NearbyDevice.DisplayName),
                nameof(NearbyDevice.Status),
                nameof(NearbyDevice.Role),
            };

            // Act
            var settable = properties
                .Select(name => typeof(NearbyDevice).GetProperty(name)!)
                .Where(p => p.SetMethod is { ReturnParameter: var ret }
                    && !ret.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"))
                .Select(p => p.Name)
                .ToArray();

            // Assert
            Assert.IsEmpty(settable, "NearbyDevice is a snapshot: every property must be init-only.");
        }

        [TestMethod]
        public void ToString_ReportsNameIdAndStatus()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice") { Status = NearbyDeviceStatus.Connected };

            // Act
            var result = device.ToString();

            // Assert
            Assert.AreEqual("Alice [ep1] Connected", result);
        }

        [TestMethod]
        public void ToString_UnnamedDevice_SaysSo()
        {
            // Arrange
            var device = new NearbyDevice("ep1", displayName: null);

            // Act
            var result = device.ToString();

            // Assert
            Assert.AreEqual("(unnamed) [ep1] Visible", result);
        }
    }
}
