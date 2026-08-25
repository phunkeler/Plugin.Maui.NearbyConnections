namespace Plugin.Maui.NearbyConnections.UnitTests;

[Trait("Category", "Transfer")]
public class NearbyTransferProgressTests
{
    public sealed class Fraction : NearbyTransferProgressTests
    {
        [Fact]
        public void KnownSize_NoProgress_ReturnsZero()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 0, totalBytes: 1000, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.Equal(0.0, result);
        }

        [Fact]
        public void KnownSize_PartialProgress_ReturnsFraction()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 500, totalBytes: 1000, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.Equal(0.5, result);
        }

        [Fact]
        public void KnownSize_Complete_ReturnsOne()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 1000, totalBytes: 1000, NearbyTransferStatus.Success);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.Equal(1.0, result);
        }

        [Fact]
        public void UnknownSize_ReturnsNull()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 500, totalBytes: -1, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ZeroTotalBytes_ReturnsNull()
        {
            // Arrange
            var progress = new NearbyTransferProgress(1, bytesTransferred: 0, totalBytes: 0, NearbyTransferStatus.InProgress);

            // Act
            var result = progress.Fraction;

            // Assert
            Assert.Null(result);
        }
    }
}
