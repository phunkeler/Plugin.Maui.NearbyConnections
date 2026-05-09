namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class NearbyConnectionsExceptionTests
{
    [TestClass]
    public sealed class NearbyAdvertisingExceptionTests : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        public void PreservesMessage()
        {
            var ex = new NearbyAdvertisingException("failed");
            Assert.AreEqual("failed", ex.Message);
        }

        [TestMethod]
        public void PreservesInnerException()
        {
            var inner = new InvalidOperationException("root cause");
            var ex = new NearbyAdvertisingException("failed", inner);
            Assert.AreSame(inner, ex.InnerException);
        }

        [TestMethod]
        public void IsCatchableAsNearbyConnectionsException()
        {
            var ex = new NearbyAdvertisingException("failed");
            Assert.IsInstanceOfType<NearbyConnectionsException>(ex);
        }
    }

    [TestClass]
    public sealed class NearbyDiscoveryExceptionTests : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        public void PreservesMessage()
        {
            var ex = new NearbyDiscoveryException("failed");
            Assert.AreEqual("failed", ex.Message);
        }

        [TestMethod]
        public void PreservesInnerException()
        {
            var inner = new InvalidOperationException("root cause");
            var ex = new NearbyDiscoveryException("failed", inner);
            Assert.AreSame(inner, ex.InnerException);
        }

        [TestMethod]
        public void IsCatchableAsNearbyConnectionsException()
        {
            var ex = new NearbyDiscoveryException("failed");
            Assert.IsInstanceOfType<NearbyConnectionsException>(ex);
        }
    }
}
