namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers inbound file naming: two peers sending the same filename must not overwrite each other.
/// </summary>
/// <remarks>
/// The bug this guards against was silent — the second <c>photo.jpg</c> replaced the first, the app
/// saw one file where it expected two, and nothing logged an error. Both platforms route inbound
/// files through this method.
/// </remarks>
[TestCategory("Connections")]
public class ResolveUniqueDestinationPathTests
{
    [TestClass]
    public sealed class WhenNameIsFree : ResolveUniqueDestinationPathTests
    {
        [TestMethod]
        public void ReturnsTheNameUnchanged()
        {
            // Arrange
            using var temp = new TempDirectory();
            var dir = temp.Path;

            // Act
            var result = PlatformNearby.ResolveUniqueDestinationPath(dir, "photo.jpg");

            // Assert
            Assert.AreEqual(Path.Combine(dir, "photo.jpg"), result);
        }
    }

    [TestClass]
    public sealed class WhenNameIsTaken : ResolveUniqueDestinationPathTests
    {
        [TestMethod]
        public void AppendsACounterBeforeTheExtension()
        {
            // " (1)" goes before ".jpg", not after — otherwise the file loses its extension and
            // the OS no longer knows how to open it.

            // Arrange
            using var temp = new TempDirectory();
            var dir = temp.Path;
            temp.Touch("photo.jpg");

            // Act
            var result = PlatformNearby.ResolveUniqueDestinationPath(dir, "photo.jpg");

            // Assert
            Assert.AreEqual(Path.Combine(dir, "photo (1).jpg"), result);
        }

        [TestMethod]
        public void CountsUpPastMultipleCollisions()
        {
            // Arrange
            using var temp = new TempDirectory();
            var dir = temp.Path;
            temp.Touch("photo.jpg");
            temp.Touch(Path.Combine(dir, "photo (1).jpg"));
            temp.Touch(Path.Combine(dir, "photo (2).jpg"));

            // Act
            var result = PlatformNearby.ResolveUniqueDestinationPath(dir, "photo.jpg");

            // Assert
            Assert.AreEqual(Path.Combine(dir, "photo (3).jpg"), result);
        }

        [TestMethod]
        public void HandlesNamesWithNoExtension()
        {
            // Arrange
            using var temp = new TempDirectory();
            var dir = temp.Path;
            temp.Touch("README");

            // Act
            var result = PlatformNearby.ResolveUniqueDestinationPath(dir, "README");

            // Assert
            Assert.AreEqual(Path.Combine(dir, "README (1)"), result);
        }

        [TestMethod]
        public void HandlesNamesWithMultipleDots()
        {
            // Only the final segment is the extension: "archive.tar.gz" must become
            // "archive.tar (1).gz", matching Path.GetExtension semantics.

            // Arrange
            using var temp = new TempDirectory();
            var dir = temp.Path;
            temp.Touch("archive.tar.gz");

            // Act
            var result = PlatformNearby.ResolveUniqueDestinationPath(dir, "archive.tar.gz");

            // Assert
            Assert.AreEqual(Path.Combine(dir, "archive.tar (1).gz"), result);
        }

        [TestMethod]
        public void ResultIsAlwaysAFreePath()
        {
            // The property that actually matters: whatever is returned must not already exist, or
            // the caller silently overwrites a file it did not create.

            // Arrange
            using var temp = new TempDirectory();
            var dir = temp.Path;
            for (var i = 0; i < 10; i++)
            {
                    var next = PlatformNearby.ResolveUniqueDestinationPath(dir, "photo.jpg");

                    Assert.IsFalse(File.Exists(next), $"Iteration {i} returned an existing path: {next}");

                    // Simulate the caller writing the file, so the next call must pick a new name.

            // Act
                    temp.Touch(next);
            }

            // Assert
            Assert.HasCount(10, Directory.GetFiles(dir), "Ten transfers should have produced ten distinct files.");
        }
    }
}
