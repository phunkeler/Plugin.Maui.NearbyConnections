using System.Text;
using Microsoft.Maui.Storage;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers sending a <see cref="FileResult"/> whose path is not readable, which is what a picker
/// returns on iOS.
/// </summary>
/// <remarks>
/// The library stages such a file itself. Before this existed, every consumer had to reconstruct
/// the copy by hand, and the sample application did exactly that.
/// </remarks>
[TestCategory("Connections")]
public class SendFileStagingTests
{
    [TestClass]
    public sealed class WhenThePathIsReadable : SendFileStagingTests
    {
        [TestMethod]
        public async Task SendsItDirectly()
        {
            // Arrange
            using var temp = new TempDirectory();
            var real = temp.Touch("photo.jpg");
            var sent = new List<string>();
            var connection = Create.Connection(sendFile: (path, _, _) =>
            {
                sent.Add(path);
                return Task.CompletedTask;
            });

            // Act
            await connection.SendAsync(new FileResult(real), cancellationToken: TestContext.CancellationToken);

            // Assert
            Assert.AreEqual(real, sent.Single());
        }

        public TestContext TestContext { get; set; }
    }

    [TestClass]
    public sealed class WhenStagingIsNeeded : SendFileStagingTests
    {
        [TestMethod]
        public async Task CopiesTheContent()
        {
            // Arrange
            var expected = "payload bytes";

            // Act
            var path = await NearbyConnection.StageToTempAsync(
                () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(expected))),
                "photo.jpg",
                TestContext.CancellationToken);

            // Assert
            Assert.AreEqual(expected, File.ReadAllText(path));
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }

        [TestMethod]
        public async Task KeepsTheOriginalFileName()
        {
            // iOS sends the last path component as the resource name, so a random temporary name
            // would reach the receiver in place of the real one.

            // Arrange
            var expected = "photo.jpg";

            // Act
            var path = await NearbyConnection.StageToTempAsync(
                () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
                expected,
                TestContext.CancellationToken);

            // Assert
            Assert.AreEqual(expected, Path.GetFileName(path));
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }

        [TestMethod]
        public async Task GivesConcurrentSendsOfTheSameNameSeparateDirectories()
        {
            // Arrange
            var name = "photo.jpg";

            // Act
            var firstPath = await NearbyConnection.StageToTempAsync(
                () => Task.FromResult<Stream>(new MemoryStream([1])),
                name,
                TestContext.CancellationToken);
            var secondPath = await NearbyConnection.StageToTempAsync(
                () => Task.FromResult<Stream>(new MemoryStream([2])),
                name,
                TestContext.CancellationToken);

            // Assert
            Assert.AreNotEqual(firstPath, secondPath);
            Directory.Delete(Path.GetDirectoryName(firstPath)!, recursive: true);
            Directory.Delete(Path.GetDirectoryName(secondPath)!, recursive: true);
        }

        [TestMethod]
        public async Task StripsADirectoryComponentFromTheName()
        {
            // Arrange
            var expected = "evil.jpg";

            // Act
            var path = await NearbyConnection.StageToTempAsync(
                () => Task.FromResult<Stream>(new MemoryStream([1])),
                "../evil.jpg",
                TestContext.CancellationToken);

            // Assert
            Assert.AreEqual(Path.Combine(Path.GetDirectoryName(path)!, expected), path);
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }

        public TestContext TestContext { get; set; }
    }
}
