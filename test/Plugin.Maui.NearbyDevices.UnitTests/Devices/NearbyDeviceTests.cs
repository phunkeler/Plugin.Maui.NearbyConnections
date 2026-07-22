namespace Plugin.Maui.NearbyDevices.UnitTests;

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
}
