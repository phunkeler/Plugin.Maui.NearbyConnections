namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the staging sweep that runs at session disposal, so a file the consumer never moved out
/// does not outlive the session that received it.
/// </summary>
/// <remarks>
/// The sweep is best-effort on purpose: disposal does not await the Android payload completion
/// chain, so a cancelled copy may still be unwinding while it runs. These tests pin that it never
/// throws and never stops early.
/// </remarks>
[TestCategory("Connections")]
public class SweepStagingDirectoryTests
{
    [TestClass]
    public sealed class WhenFilesAreStaged : SweepStagingDirectoryTests
    {
        [TestMethod]
        public void DeletesThemAll()
        {
            // Arrange
            using var temp = new TempDirectory();
            var platform = Create.PlatformNearby();
            temp.Touch("photo.jpg");
            temp.Touch("clip.mp4");

            // Act
            platform.SweepStagingDirectory(temp.Path);

            // Assert
            Assert.IsEmpty(Directory.GetFiles(temp.Path));
        }
    }

    [TestClass]
    public sealed class WhenTheDirectoryIsMissing : SweepStagingDirectoryTests
    {
        [TestMethod]
        public void ReturnsQuietly()
        {
            // A session that never received a file never creates the directory, and disposal must
            // not fault on that.

            // Arrange
            using var temp = new TempDirectory();
            var platform = Create.PlatformNearby();
            var missing = Path.Combine(temp.Path, "never-created");

            // Act
            platform.SweepStagingDirectory(missing);

            // Assert
            Assert.IsFalse(Directory.Exists(missing));
        }
    }

    [TestClass]
    public sealed class WhenOneFileCannotBeDeleted : SweepStagingDirectoryTests
    {
        [TestMethod]
        public void StillDeletesTheRest()
        {
            // Arrange
            using var temp = new TempDirectory();
            var platform = Create.PlatformNearby();
            var locked = temp.Touch("locked.bin");
            temp.Touch("free.bin");
            using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

            // Act
            platform.SweepStagingDirectory(temp.Path);

            // Assert
            Assert.IsFalse(File.Exists(Path.Combine(temp.Path, "free.bin")));
        }
    }
}
