using Microsoft.Maui.Storage;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the keep affordance on a received file payload.
/// </summary>
/// <remarks>
/// The move is what both keeps the file and clears it from staging, so the tests that matter are
/// the ones asserting the staging path is gone afterwards.
/// </remarks>
[Trait("Category", "Payload")]
public class NearbyFilePayloadTests
{
    public sealed class WhenTheDestinationIsFree : NearbyFilePayloadTests
    {
        [Fact]
        public void MovesTheContent()
        {
            // Arrange
            using var temp = new TempDirectory();
            var staged = Path.Combine(temp.Path, "photo.jpg");
            File.WriteAllText(staged, "bytes");
            var payload = new NearbyFilePayload(new FileResult(staged));
            var destination = Path.Combine(temp.Path, "kept", "photo.jpg");

            // Act
            var result = payload.MoveTo(destination);

            // Assert
            Assert.Equal("bytes", File.ReadAllText(destination));
            Assert.Equal(destination, result.FullPath);
        }

        [Fact]
        public void ClearsTheStagedFile()
        {
            // This is the reason the method exists: reading the stream and copying would leave the
            // staged original behind.

            // Arrange
            using var temp = new TempDirectory();
            var staged = Path.Combine(temp.Path, "photo.jpg");
            File.WriteAllText(staged, "bytes");
            var payload = new NearbyFilePayload(new FileResult(staged));

            // Act
            payload.MoveTo(Path.Combine(temp.Path, "kept", "photo.jpg"));

            // Assert
            Assert.False(File.Exists(staged));
        }

        [Fact]
        public void CreatesAMissingDestinationDirectory()
        {
            // Arrange
            using var temp = new TempDirectory();
            var staged = Path.Combine(temp.Path, "photo.jpg");
            File.WriteAllText(staged, "bytes");
            var payload = new NearbyFilePayload(new FileResult(staged));
            var destination = Path.Combine(temp.Path, "a", "b", "photo.jpg");

            // Act
            payload.MoveTo(destination);

            // Assert
            Assert.True(File.Exists(destination));
        }
    }

    public sealed class WhenTheDestinationIsTaken : NearbyFilePayloadTests
    {
        [Fact]
        public void ThrowsAndLeavesTheStagedFileInPlace()
        {
            // Arrange
            using var temp = new TempDirectory();
            var staged = Path.Combine(temp.Path, "photo.jpg");
            File.WriteAllText(staged, "bytes");
            var payload = new NearbyFilePayload(new FileResult(staged));
            var destination = temp.Touch("taken.jpg");

            // Act
            Assert.Throws<IOException>(() => payload.MoveTo(destination));

            // Assert
            Assert.True(File.Exists(staged));
        }
    }

    public sealed class WhenTheStagedFileIsGone : NearbyFilePayloadTests
    {
        [Fact]
        public void Throws()
        {
            // The operating system may purge staging, and a second move finds nothing left.

            // Arrange
            using var temp = new TempDirectory();
            var payload = new NearbyFilePayload(new FileResult(Path.Combine(temp.Path, "purged.jpg")));

            // Act
            var act = () => payload.MoveTo(Path.Combine(temp.Path, "kept.jpg"));

            // Assert
            Assert.Throws<FileNotFoundException>(act);
        }
    }

    public sealed class WhenTheDestinationIsNull : NearbyFilePayloadTests
    {
        [Fact]
        public void Throws()
        {
            // Arrange
            using var temp = new TempDirectory();
            var payload = new NearbyFilePayload(new FileResult(temp.Touch("photo.jpg")));

            // Act
            var act = () => payload.MoveTo(null!);

            // Assert
            Assert.Throws<ArgumentNullException>(act);
        }
    }
}
