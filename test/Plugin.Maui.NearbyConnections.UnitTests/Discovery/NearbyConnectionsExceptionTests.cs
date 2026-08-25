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
[Trait("Category", "Discovery")]
public class NearbyConnectionsExceptionTests
{
    public sealed class Construction : NearbyConnectionsExceptionTests
    {
        [Theory]
        [InlineData(typeof(NearbyException), TestDisplayName = "Base")]
        [InlineData(typeof(NearbyAdvertisingException), TestDisplayName = "Advertising")]
        [InlineData(typeof(NearbyDiscoveryException), TestDisplayName = "Discovery")]
        [InlineData(typeof(NearbyConnectionTimeoutException), TestDisplayName = "ConnectionTimeout")]
        [InlineData(typeof(NearbyTransferException), TestDisplayName = "Transfer")]
        [InlineData(typeof(NearbyTransferTimeoutException), TestDisplayName = "TransferTimeout")]
        public void MessageOnly_PreservesMessageAndDerivesFromNearbyException(Type exceptionType)
        {
            // Arrange
            var message = "failed";

            // Act
            var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.IsAssignableFrom<NearbyException>(exception);
        }

        [Theory]
        [InlineData(typeof(NearbyException), TestDisplayName = "Base")]
        [InlineData(typeof(NearbyAdvertisingException), TestDisplayName = "Advertising")]
        [InlineData(typeof(NearbyDiscoveryException), TestDisplayName = "Discovery")]
        [InlineData(typeof(NearbyTransferException), TestDisplayName = "Transfer")]
        public void MessageAndInner_PreservesBoth(Type exceptionType)
        {
            // Arrange
            var message = "failed";
            var inner = new InvalidOperationException("root cause");

            // Act
            var exception = (Exception)Activator.CreateInstance(exceptionType, message, inner)!;

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(inner, exception.InnerException);
        }
    }
}
