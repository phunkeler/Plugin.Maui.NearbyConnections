namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Every <see cref="NearbyException"/> subclass carries at least the two-constructor contract
/// (message, message + inner), so the message-only case is asserted from one set of rows across
/// every subclass: a caller catching <see cref="NearbyException"/> must be able to handle any of
/// them without knowing which operation failed. Not every subclass has a real cause to chain — the
/// two timeout types (<see cref="NearbyConnectionTimeoutException"/>,
/// <see cref="NearbyTransferTimeoutException"/>) fire on an elapsed deadline, not a caught
/// exception, so they carry no <c>(message, inner)</c> overload and are excluded from
/// <see cref="MessageAndInner_PreservesBoth"/>.
/// </summary>
[TestCategory("Discovery")]
public class NearbyConnectionsExceptionTests
{
    [TestClass]
    public sealed class Construction : NearbyConnectionsExceptionTests
    {
        [TestMethod]
        [DataRow(typeof(NearbyException), DisplayName = "Base")]
        [DataRow(typeof(NearbyAdvertisingException), DisplayName = "Advertising")]
        [DataRow(typeof(NearbyDiscoveryException), DisplayName = "Discovery")]
        [DataRow(typeof(NearbyConnectionTimeoutException), DisplayName = "ConnectionTimeout")]
        [DataRow(typeof(NearbyTransferException), DisplayName = "Transfer")]
        [DataRow(typeof(NearbyTransferTimeoutException), DisplayName = "TransferTimeout")]
        public void MessageOnly_PreservesMessageAndDerivesFromNearbyException(Type exceptionType)
        {
            // Arrange
            var message = "failed";

            // Act
            var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;

            // Assert
            Assert.AreEqual(message, exception.Message);
            Assert.IsInstanceOfType<NearbyException>(exception);
        }

        [TestMethod]
        [DataRow(typeof(NearbyException), DisplayName = "Base")]
        [DataRow(typeof(NearbyAdvertisingException), DisplayName = "Advertising")]
        [DataRow(typeof(NearbyDiscoveryException), DisplayName = "Discovery")]
        [DataRow(typeof(NearbyTransferException), DisplayName = "Transfer")]
        public void MessageAndInner_PreservesBoth(Type exceptionType)
        {
            // Arrange
            var message = "failed";
            var inner = new InvalidOperationException("root cause");

            // Act
            var exception = (Exception)Activator.CreateInstance(exceptionType, message, inner)!;

            // Assert
            Assert.AreEqual(message, exception.Message);
            Assert.AreSame(inner, exception.InnerException);
        }
    }
}
