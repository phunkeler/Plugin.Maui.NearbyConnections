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
[Trait("Category", "Options")]
public class NearbyConnectionsOptionsTests
{
    public sealed class PlatformScopes : NearbyConnectionsOptionsTests
    {
        [Fact]
        public void Scopes_AreAvailableOnEveryTargetFramework()
        {
            // Arrange
            var options = new NearbyOptions();

            // Act
            var android = options.Android;
            var apple = options.Apple;

            // Assert
            // Android options must exist on every TFM, not just android.
            Assert.NotNull(android);            // Apple options must exist on every TFM, not just ios.
            Assert.NotNull(apple);
        }

        [Fact]
        public void Scopes_AreIndependentBetweenInstances()
        {
            // Arrange
            var first = new NearbyOptions();
            var second = new NearbyOptions();

            // Act
            first.Android.Topology = NearbyTopology.Star;

            // Assert
            // The scope objects must not be shared between options instances.
            Assert.Equal(NearbyTopology.Cluster, second.Android.Topology);
        }
    }

    public sealed class Defaults : NearbyConnectionsOptionsTests
    {
        [Fact]
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
            Assert.Equal(
                (NearbyTopology.Cluster,
                 NearbyConnectionType.Balanced,
                 false,
                 NearbyEncryptionPreference.Required),
                actual);
        }

        [Fact]
        public void ConnectTimeout_IsThirtySeconds()
        {
            // Arrange
            var expected = TimeSpan.FromSeconds(30);

            // Act
            var actual = new NearbyOptions().ConnectTimeout;

            // Assert — load-bearing twice over: Google's Nearby Connections has no native
            // invitation timeout, so this default is what stops an un-configured app hanging
            // forever on Android — and it is the declared offer window, so it is also the remote
            // side's assumed window (OfferWindow.Default). The two must stay one number.
            Assert.Equal(expected, actual);
            Assert.Equal(OfferWindow.s_default, actual);
        }
    }
}
