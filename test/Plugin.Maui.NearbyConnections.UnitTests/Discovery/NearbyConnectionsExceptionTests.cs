namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class NearbyConnectionsExceptionTests
{
    [TestClass]
    public sealed class Advertising : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        public void PreservesMessage()
        {
            // Arrange
            var ex = new NearbyAdvertisingException("failed");

            // Act
            var message = ex.Message;

            // Assert
            Assert.AreEqual("failed", message);
        }

        [TestMethod]
        public void PreservesInnerException()
        {
            // Arrange
            var inner = new InvalidOperationException("root cause");
            var ex = new NearbyAdvertisingException("failed", inner);

            // Act
            var result = ex.InnerException;

            // Assert
            Assert.AreSame(inner, result);
        }

        [TestMethod]
        public void IsCatchableAsNearbyConnectionsException()
        {
            // Arrange
            var ex = new NearbyAdvertisingException("failed");

            // Act (type relationship is structural — no runtime operation required)

            // Assert
            Assert.IsInstanceOfType<NearbyException>(ex);
        }
    }

    [TestClass]
    public sealed class Discovery : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        public void PreservesMessage()
        {
            // Arrange
            var ex = new NearbyDiscoveryException("failed");

            // Act
            var message = ex.Message;

            // Assert
            Assert.AreEqual("failed", message);
        }

        [TestMethod]
        public void PreservesInnerException()
        {
            // Arrange
            var inner = new InvalidOperationException("root cause");
            var ex = new NearbyDiscoveryException("failed", inner);

            // Act
            var result = ex.InnerException;

            // Assert
            Assert.AreSame(inner, result);
        }

        [TestMethod]
        public void IsCatchableAsNearbyConnectionsException()
        {
            // Arrange
            var ex = new NearbyDiscoveryException("failed");

            // Act (type relationship is structural — no runtime operation required)

            // Assert
            Assert.IsInstanceOfType<NearbyException>(ex);
        }
    }
}
