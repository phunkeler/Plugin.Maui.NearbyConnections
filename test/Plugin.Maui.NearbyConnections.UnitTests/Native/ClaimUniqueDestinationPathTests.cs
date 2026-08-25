namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the inbound file name reservation: the claim creates the file, so a concurrent transfer
/// cannot take the same name between resolving it and writing to it.
/// </summary>
/// <remarks>
/// Two claims are genuinely concurrent in production — the Android work queue serialises
/// copies within an endpoint but not across endpoints, and iOS delegate callbacks for different
/// peers arrive on different threads. The retry these tests exercise is what makes that safe.
/// </remarks>
[Trait("Category", "Connections")]
public class ClaimUniqueDestinationPathTests
{
    public sealed class WhenTheDirectoryIsMissing : ClaimUniqueDestinationPathTests
    {
        [Fact]
        public void CreatesIt()
        {
            // Arrange
            using var temp = new TempDirectory();
            var dir = Path.Combine(temp.Path, "nearby-received");

            // Act
            using var result = PlatformBridge.ClaimUniqueDestinationPath(dir, "photo.jpg");

            // Assert
            Assert.Equal(Path.Combine(dir, "photo.jpg"), result.Name);
        }
    }

    public sealed class WhenNameIsFree : ClaimUniqueDestinationPathTests
    {
        [Fact]
        public void ClaimsItAndLeavesTheFileOnDisk()
        {
            // Arrange
            using var temp = new TempDirectory();
            var expected = Path.Combine(temp.Path, "photo.jpg");

            // Act
            using var result = PlatformBridge.ClaimUniqueDestinationPath(temp.Path, "photo.jpg");

            // Assert
            Assert.Equal(expected, result.Name);
            Assert.True(File.Exists(expected));
        }
    }

    public sealed class WhenNameIsTaken : ClaimUniqueDestinationPathTests
    {
        [Fact]
        public void ClaimsTheCounteredName()
        {
            // Arrange
            using var temp = new TempDirectory();
            temp.Touch("photo.jpg");

            // Act
            using var result = PlatformBridge.ClaimUniqueDestinationPath(temp.Path, "photo.jpg");

            // Assert
            Assert.Equal(Path.Combine(temp.Path, "photo (1).jpg"), result.Name);
        }
    }

    public sealed class WhenAConcurrentClaimHoldsTheResolvedName : ClaimUniqueDestinationPathTests
    {
        [Fact]
        public void RetriesPastIt()
        {
            // Simulates the real race: a rival holds an open claim on the name this call resolves
            // to, so CreateNew throws and the loop must re-resolve rather than fail or overwrite.

            // Arrange
            using var temp = new TempDirectory();
            using var rival = PlatformBridge.ClaimUniqueDestinationPath(temp.Path, "photo.jpg");
            var expected = Path.Combine(temp.Path, "photo (1).jpg");

            // Act
            using var result = PlatformBridge.ClaimUniqueDestinationPath(temp.Path, "photo.jpg");

            // Assert
            Assert.Equal(expected, result.Name);
        }
    }

    public sealed class WhenTheNameCarriesADirectoryComponent : ClaimUniqueDestinationPathTests
    {
        [Fact]
        public void StripsItSoTheWriteStaysInTheDirectory()
        {
            // The name arrives from a remote peer, so it must not be able to steer the write.

            // Arrange
            using var temp = new TempDirectory();
            var expected = Path.Combine(temp.Path, "evil.jpg");

            // Act
            using var result = PlatformBridge.ClaimUniqueDestinationPath(temp.Path, "../evil.jpg");

            // Assert
            Assert.Equal(expected, result.Name);
        }
    }
}
