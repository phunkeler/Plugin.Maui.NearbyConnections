namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Both start-failure exceptions carry the same contract, so they are asserted from one set of
/// rows: a caller catching <see cref="NearbyException"/> must handle either without knowing which
/// operation failed.
/// </summary>
[TestCategory("Discovery")]
public class NearbyConnectionsExceptionTests
{
    [TestClass]
    public sealed class Construction : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        [DataRow(typeof(NearbyAdvertisingException), DisplayName = "Advertising")]
        [DataRow(typeof(NearbyDiscoveryException), DisplayName = "Discovery")]
        public void MessageOnly_PreservesMessageAndDerivesFromNearbyException(Type exceptionType)
        {
            // Arrange
            const string Message = "failed";

            // Act
            var exception = (Exception)Activator.CreateInstance(exceptionType, Message)!;

            // Assert
            Assert.AreEqual(Message, exception.Message);
            Assert.IsInstanceOfType<NearbyException>(exception);
        }

        [TestMethod]
        [DataRow(typeof(NearbyAdvertisingException), DisplayName = "Advertising")]
        [DataRow(typeof(NearbyDiscoveryException), DisplayName = "Discovery")]
        public void MessageAndInner_PreservesBoth(Type exceptionType)
        {
            // Arrange
            const string Message = "failed";
            var inner = new InvalidOperationException("root cause");

            // Act
            var exception = (Exception)Activator.CreateInstance(exceptionType, Message, inner)!;

            // Assert
            Assert.AreEqual(Message, exception.Message);
            Assert.AreSame(inner, exception.InnerException);
        }
    }
}
