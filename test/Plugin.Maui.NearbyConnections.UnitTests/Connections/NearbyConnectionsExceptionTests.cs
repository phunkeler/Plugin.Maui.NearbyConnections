namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class NearbyConnectionsExceptionTests
{
    static readonly NearbyConnectionsOptions Options = new();

    [TestClass]
    public sealed class NearbyAdvertisingExceptionTests : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        public void PreservesOptions()
        {
            // Arrange & Act
            var ex = new NearbyAdvertisingException(Options, "failed");

            // Assert
            Assert.AreSame(Options, ex.Options);
        }

        [TestMethod]
        public void PreservesMessage()
        {
            // Arrange & Act
            var ex = new NearbyAdvertisingException(Options, "failed");

            // Assert
            Assert.AreEqual("failed", ex.Message);
        }

        [TestMethod]
        public void PreservesInnerException()
        {
            // Arrange
            var inner = new InvalidOperationException("root cause");

            // Act
            var ex = new NearbyAdvertisingException(Options, "failed", inner);

            // Assert
            Assert.AreSame(inner, ex.InnerException);
        }

        [TestMethod]
        public void IsCatchableAsNearbyConnectionsException()
        {
            // Arrange
            var ex = new NearbyAdvertisingException(Options, "failed");

            // Act & Assert
            Assert.IsInstanceOfType<NearbyConnectionsException>(ex);
        }
    }

    [TestClass]
    public sealed class NearbyDiscoveryExceptionTests : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        public void PreservesOptions()
        {
            // Arrange & Act
            var ex = new NearbyDiscoveryException(Options, "failed");

            // Assert
            Assert.AreSame(Options, ex.Options);
        }

        [TestMethod]
        public void PreservesMessage()
        {
            // Arrange & Act
            var ex = new NearbyDiscoveryException(Options, "failed");

            // Assert
            Assert.AreEqual("failed", ex.Message);
        }

        [TestMethod]
        public void PreservesInnerException()
        {
            // Arrange
            var inner = new InvalidOperationException("root cause");

            // Act
            var ex = new NearbyDiscoveryException(Options, "failed", inner);

            // Assert
            Assert.AreSame(inner, ex.InnerException);
        }

        [TestMethod]
        public void IsCatchableAsNearbyConnectionsException()
        {
            // Arrange
            var ex = new NearbyDiscoveryException(Options, "failed");

            // Act & Assert
            Assert.IsInstanceOfType<NearbyConnectionsException>(ex);
        }
    }
}
