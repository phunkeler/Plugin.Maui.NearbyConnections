namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the platform-scoped option objects on <see cref="NearbyOptions"/>.
/// </summary>
/// <remarks>
/// These settings previously existed only on the target framework that consumed them, so shared
/// MAUI code could not set them without <c>#if</c>. They are now present on every target framework
/// and named by platform at the call site. This suite runs on <c>net10.0</c> — the target that
/// consumes none of them — so it fails if either scope is ever made conditional again.
/// </remarks>
[TestCategory("Options")]
public class NearbyConnectionsOptionsTests
{
    [TestClass]
    public sealed class PlatformScopes : NearbyConnectionsOptionsTests
    {
        [TestMethod]
        public void Scopes_AreAvailableOnEveryTargetFramework()
        {
            // Arrange
            var options = new NearbyOptions();

            // Act
            var android = options.Android;
            var apple = options.Apple;

            // Assert
            Assert.IsNotNull(android, "Android options must exist on every TFM, not just android.");
            Assert.IsNotNull(apple, "Apple options must exist on every TFM, not just ios.");
        }

        [TestMethod]
        public void Scopes_AreSettableOnEveryTargetFramework()
        {
            // Arrange
            var options = new NearbyOptions();
            const NearbyTopology expectedTopology = NearbyTopology.PointToPoint;
            const NearbyConnectionType expectedConnectionType = NearbyConnectionType.HighBandwidth;
            const NearbyEncryptionPreference expectedEncryption = NearbyEncryptionPreference.Optional;

            // Act
            options.Android.Topology = expectedTopology;
            options.Android.ConnectionType = expectedConnectionType;
            options.Android.UseLowPower = true;
            options.Apple.EncryptionPreference = expectedEncryption;

            // Assert
            Assert.AreEqual(expectedTopology, options.Android.Topology);
            Assert.AreEqual(expectedConnectionType, options.Android.ConnectionType);
            Assert.IsTrue(options.Android.UseLowPower);
            Assert.AreEqual(expectedEncryption, options.Apple.EncryptionPreference);
        }

        [TestMethod]
        public void Scopes_AreIndependentBetweenInstances()
        {
            // Arrange
            var first = new NearbyOptions();
            var second = new NearbyOptions();

            // Act
            first.Android.Topology = NearbyTopology.Star;

            // Assert
            Assert.AreEqual(
                NearbyTopology.Cluster,
                second.Android.Topology,
                "The scope objects must not be shared between options instances.");
        }
    }

    [TestClass]
    public sealed class Defaults : NearbyConnectionsOptionsTests
    {
        [TestMethod]
        public void PlatformScopes_CarryTheDocumentedDefaults()
        {
            // Arrange
            var options = new NearbyOptions();

            // Act
            var actual = (
                options.Android.Topology,
                options.Android.ConnectionType,
                options.Android.UseLowPower,
                options.Apple.EncryptionPreference);

            // Assert
            Assert.AreEqual(
                (NearbyTopology.Cluster,
                 NearbyConnectionType.Balanced,
                 false,
                 NearbyEncryptionPreference.Required),
                actual);
        }
    }
}
