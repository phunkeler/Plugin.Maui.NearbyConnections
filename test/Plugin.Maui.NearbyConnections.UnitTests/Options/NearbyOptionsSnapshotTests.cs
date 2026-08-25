namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers <see cref="NearbyOptions.Snapshot"/> — the copy <c>AddNearby</c> captures after
/// validation, so a caller who kept the configured instance cannot mutate the session's
/// configuration (the C5 row for configured options).
/// </summary>
[Trait("Category", "Options")]
public class NearbyOptionsSnapshotTests
{
    public sealed class Scalars : NearbyOptionsSnapshotTests
    {
        [Fact]
        public void Snapshot_CopiesEveryScalar()
        {
            // Arrange
            var options = new NearbyOptions
            {
                DisplayName = "kiosk-7",
                ServiceId = "nearbychat",
                DiscoveryRefreshInterval = TimeSpan.FromSeconds(5),
                ConnectTimeout = TimeSpan.FromSeconds(1),
                AcceptTimeout = TimeSpan.FromSeconds(2),
                InboundRequestTimeout = TimeSpan.FromSeconds(3),
                TransferInactivityTimeout = TimeSpan.FromSeconds(4),
                AutoAcceptConnectionRequests = true,
            };

            // Act
            var snapshot = options.Snapshot();

            // Assert
            Assert.Equal("kiosk-7", snapshot.DisplayName);
            Assert.Equal("nearbychat", snapshot.ServiceId);
            Assert.Equal(TimeSpan.FromSeconds(5), snapshot.DiscoveryRefreshInterval);
            Assert.Equal(TimeSpan.FromSeconds(1), snapshot.ConnectTimeout);
            Assert.Equal(TimeSpan.FromSeconds(2), snapshot.AcceptTimeout);
            Assert.Equal(TimeSpan.FromSeconds(3), snapshot.InboundRequestTimeout);
            Assert.Equal(TimeSpan.FromSeconds(4), snapshot.TransferInactivityTimeout);
            Assert.True(snapshot.AutoAcceptConnectionRequests);
        }

        [Fact]
        public void Snapshot_IsUnaffectedByLaterMutation()
        {
            // Arrange
            var options = new NearbyOptions { ServiceId = "before" };
            var snapshot = options.Snapshot();

            // Act
            options.ServiceId = "after";
            options.AutoAcceptConnectionRequests = true;

            // Assert
            Assert.Equal("before", snapshot.ServiceId);
            Assert.False(snapshot.AutoAcceptConnectionRequests);
        }
    }

    public sealed class PlatformScopes : NearbyOptionsSnapshotTests
    {
        [Fact]
        public void Snapshot_CopiesBothPlatformScopes()
        {
            // Arrange
            var options = new NearbyOptions();
            options.Android.Topology = NearbyTopology.Star;
            options.Android.UseLowPower = true;
            options.Android.ConnectionType = NearbyConnectionType.HighBandwidth;
            options.Apple.EncryptionPreference = NearbyEncryptionPreference.Optional;
            options.Apple.StartFailureGraceWindow = TimeSpan.FromSeconds(2);

            // Act
            var snapshot = options.Snapshot();

            // Assert
            Assert.Equal(NearbyTopology.Star, snapshot.Android.Topology);
            Assert.True(snapshot.Android.UseLowPower);
            Assert.Equal(NearbyConnectionType.HighBandwidth, snapshot.Android.ConnectionType);
            Assert.Equal(NearbyEncryptionPreference.Optional, snapshot.Apple.EncryptionPreference);
            Assert.Equal(TimeSpan.FromSeconds(2), snapshot.Apple.StartFailureGraceWindow);
        }

        [Fact]
        public void Snapshot_DoesNotShareScopeInstances()
        {
            // Arrange
            var options = new NearbyOptions();
            var snapshot = options.Snapshot();

            // Act
            options.Android.Topology = NearbyTopology.PointToPoint;

            // Assert
            // The snapshot's scope objects must be copies, not the caller's instances.
            Assert.Equal(NearbyTopology.Cluster, snapshot.Android.Topology);
        }
    }
}
