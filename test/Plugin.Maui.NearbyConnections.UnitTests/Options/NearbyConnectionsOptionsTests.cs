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

        [TestMethod]
        public void ConnectTimeout_IsThirtySeconds()
        {
            // Arrange
            var expected = TimeSpan.FromSeconds(30);

            // Act
            var actual = new NearbyOptions().ConnectTimeout;

            // Assert — load-bearing: Google's Nearby Connections has no native invitation timeout,
            // so this default is what stops an un-configured app hanging forever on Android.
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AcceptTimeout_IsFifteenSeconds()
        {
            // Arrange
            var expected = TimeSpan.FromSeconds(15);

            // Act
            var actual = new NearbyOptions().AcceptTimeout;

            // Assert — deliberately shorter than ConnectTimeout: the accept window excludes the
            // remote user's decision, so only the handshake remains.
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void InboundRequestTimeout_IsThirtySeconds()
        {
            // Arrange
            var expected = TimeSpan.FromSeconds(30);

            // Act
            var actual = new NearbyOptions().InboundRequestTimeout;

            // Assert
            Assert.AreEqual(expected, actual);
        }
    }
}
