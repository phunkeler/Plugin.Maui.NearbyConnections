namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// MPC's "did not start" delegate callbacks fault the corresponding channel with the typed
/// exception, so a consumer enumerating <c>AdvertiseAsync</c>/<c>DiscoverAsync</c> observes a
/// <see cref="NearbyAdvertisingException"/>/<see cref="NearbyDiscoveryException"/> rather than a
/// silent end-of-stream.
/// </summary>
public class StartFailureTests : DeviceTest
{
    [Fact]
    public async Task DidNotStartAdvertising_FaultsAdvertiseChannel()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var error = new NSError((NSString)"devtest", code: 42);

        // Act
        platform.DidNotStartAdvertisingPeer(advertiser: null!, error);

        // Assert
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<NearbyAdvertisingException>(
            () => platform._advertiseChannel.Reader.Completion.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task DidNotStartBrowsing_FaultsDiscoverChannel()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        using var error = new NSError((NSString)"devtest", code: 42);

        // Act
        platform.DidNotStartBrowsingForPeers(browser: null!, error);

        // Assert
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<NearbyDiscoveryException>(
            () => platform._discoverChannel.Reader.Completion.WaitAsync(cts.Token));
    }
}
