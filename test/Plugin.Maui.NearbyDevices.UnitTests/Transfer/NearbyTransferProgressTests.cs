namespace Plugin.Maui.NearbyDevices.UnitTests;

[TestCategory("Transfer")]
public class NearbyTransferProgressTests
{
    [TestClass]
    public sealed class Fraction : NearbyTransferProgressTests
    {
        [TestMethod]
        public void KnownSize_NoProgress_ReturnsZero()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 0, totalBytes: 1000, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.AreEqual(0.0, result);
        }

        [TestMethod]
        public void KnownSize_PartialProgress_ReturnsFraction()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 500, totalBytes: 1000, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.AreEqual(0.5, result);
        }

        [TestMethod]
        public void KnownSize_Complete_ReturnsOne()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 1000, totalBytes: 1000, NearbyTransferStatus.Success);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.AreEqual(1.0, result);
        }

        [TestMethod]
        public void UnknownSize_ReturnsNull()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 500, totalBytes: -1, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ZeroTotalBytes_ReturnsNull()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 0, totalBytes: 0, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.IsNull(result);
        }
    }
}
