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
    /// <summary>A real temp directory per test; inbound naming genuinely touches the file system.</summary>
    static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"nc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void Touch(string path) => File.WriteAllText(path, string.Empty);

    [TestClass]
    public sealed class WhenNameIsFree : ResolveUniqueDestinationPathTests
    {
        [TestMethod]
        public void ReturnsTheNameUnchanged()
        {
            var dir = CreateTempDirectory();

            try
            {
                var result = NearbyConnectionsImplementation.ResolveUniqueDestinationPath(dir, "photo.jpg");

                Assert.AreEqual(Path.Combine(dir, "photo.jpg"), result);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
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
            var dir = CreateTempDirectory();

            try
            {
                Touch(Path.Combine(dir, "photo.jpg"));

                var result = NearbyConnectionsImplementation.ResolveUniqueDestinationPath(dir, "photo.jpg");

                Assert.AreEqual(Path.Combine(dir, "photo (1).jpg"), result);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void CountsUpPastMultipleCollisions()
        {
            var dir = CreateTempDirectory();

            try
            {
                Touch(Path.Combine(dir, "photo.jpg"));
                Touch(Path.Combine(dir, "photo (1).jpg"));
                Touch(Path.Combine(dir, "photo (2).jpg"));

                var result = NearbyConnectionsImplementation.ResolveUniqueDestinationPath(dir, "photo.jpg");

                Assert.AreEqual(Path.Combine(dir, "photo (3).jpg"), result);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void HandlesNamesWithNoExtension()
        {
            var dir = CreateTempDirectory();

            try
            {
                Touch(Path.Combine(dir, "README"));

                var result = NearbyConnectionsImplementation.ResolveUniqueDestinationPath(dir, "README");

                Assert.AreEqual(Path.Combine(dir, "README (1)"), result);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void HandlesNamesWithMultipleDots()
        {
            // Only the final segment is the extension: "archive.tar.gz" must become
            // "archive.tar (1).gz", matching Path.GetExtension semantics.
            var dir = CreateTempDirectory();

            try
            {
                Touch(Path.Combine(dir, "archive.tar.gz"));

                var result = NearbyConnectionsImplementation.ResolveUniqueDestinationPath(dir, "archive.tar.gz");

                Assert.AreEqual(Path.Combine(dir, "archive.tar (1).gz"), result);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void ResultIsAlwaysAFreePath()
        {
            // The property that actually matters: whatever is returned must not already exist, or
            // the caller silently overwrites a file it did not create.
            var dir = CreateTempDirectory();

            try
            {
                for (var i = 0; i < 10; i++)
                {
                    var next = NearbyConnectionsImplementation.ResolveUniqueDestinationPath(dir, "photo.jpg");

                    Assert.IsFalse(File.Exists(next), $"Iteration {i} returned an existing path: {next}");

                    // Simulate the caller writing the file, so the next call must pick a new name.
                    Touch(next);
                }

                Assert.HasCount(10, Directory.GetFiles(dir), "Ten transfers should have produced ten distinct files.");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
