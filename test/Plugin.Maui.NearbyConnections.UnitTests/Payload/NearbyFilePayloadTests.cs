using Microsoft.Maui.Storage;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the keep affordance on a received file payload.
/// </summary>
/// <remarks>
/// The move is what both keeps the file and clears it from staging, so the tests that matter are
/// the ones asserting the staging path is gone afterwards.
/// </remarks>
[TestCategory("Payload")]
public class NearbyFilePayloadTests
{
    [TestClass]
    public sealed class WhenTheDestinationIsFree : NearbyFilePayloadTests
    {
        [TestMethod]
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
            Assert.AreEqual("bytes", File.ReadAllText(destination));
            Assert.AreEqual(destination, result.FullPath);
        }

        [TestMethod]
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
            Assert.IsFalse(File.Exists(staged));
        }

        [TestMethod]
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
            Assert.IsTrue(File.Exists(destination));
        }
    }

    [TestClass]
    public sealed class WhenTheDestinationIsTaken : NearbyFilePayloadTests
    {
        [TestMethod]
        public void ThrowsAndLeavesTheStagedFileInPlace()
        {
            // Arrange
            using var temp = new TempDirectory();
            var staged = Path.Combine(temp.Path, "photo.jpg");
            File.WriteAllText(staged, "bytes");
            var payload = new NearbyFilePayload(new FileResult(staged));
            var destination = temp.Touch("taken.jpg");

            // Act
            Assert.ThrowsExactly<IOException>(() => payload.MoveTo(destination));

            // Assert
            Assert.IsTrue(File.Exists(staged));
        }
    }

    [TestClass]
    public sealed class WhenTheStagedFileIsGone : NearbyFilePayloadTests
    {
        [TestMethod]
        public void Throws()
        {
            // The operating system may purge staging, and a second move finds nothing left.

            // Arrange
            using var temp = new TempDirectory();
            var payload = new NearbyFilePayload(new FileResult(Path.Combine(temp.Path, "purged.jpg")));

            // Act
            var act = () => payload.MoveTo(Path.Combine(temp.Path, "kept.jpg"));

            // Assert
            Assert.ThrowsExactly<FileNotFoundException>(act);
        }
    }

    [TestClass]
    public sealed class WhenTheDestinationIsNull : NearbyFilePayloadTests
    {
        [TestMethod]
        public void Throws()
        {
            // Arrange
            using var temp = new TempDirectory();
            var payload = new NearbyFilePayload(new FileResult(temp.Touch("photo.jpg")));

            // Act
            var act = () => payload.MoveTo(null!);

            // Assert
            Assert.ThrowsExactly<ArgumentNullException>(act);
        }
    }
}
