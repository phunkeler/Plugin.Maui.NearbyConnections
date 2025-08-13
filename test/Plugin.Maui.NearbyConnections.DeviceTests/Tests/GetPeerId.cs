#if IOS
using Plugin.Maui.NearbyConnections;
using Xunit;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Tests;

public class GetPeerIdTests
{
    [Fact]
    public void TestGetPeerId()
    {
        var _sut = new NearbyConnectionsManager();
        var peerId = _sut.GetPeerId("Test");

        Assert.NotNull(peerId);
    }
}

public class IOSAdvertiserTests
{
    [Fact]
    public async Task TestAdvertiserLifecycle()
    {
        // Arrange
        var advertiser = new NearbyConnectionsAdvertiser();
        var options = new TestAdvertisingOptions
        {
            ServiceName = "testservice",
            DiscoveryInfo = new Dictionary<string, string>
            {
                ["device_type"] = "test",
                ["app_version"] = "1.0.0"
            }
        };

        bool stateChanged = false;
        advertiser.AdvertisingStateChanged += (s, e) => stateChanged = true;

        // Act & Assert - Start advertising
        Assert.False(advertiser.IsAdvertising);

        await advertiser.StartAdvertisingAsync(options);
        Assert.True(advertiser.IsAdvertising);
        Assert.True(stateChanged);
    }

    [Fact]
    public async Task TestAdvertiserIdempotentOperations()
    {
        // Arrange
        var advertiser = new NearbyConnectionsAdvertiser();
        var options = new TestAdvertisingOptions
        {
            ServiceName = "testservice",
            DiscoveryInfo = new Dictionary<string, string>()
        };

        // Act - Start multiple times
        await advertiser.StartAdvertisingAsync(options);
        await advertiser.StartAdvertisingAsync(options); // Should be no-op

        Assert.True(advertiser.IsAdvertising);

        // Act - Stop multiple times
        await advertiser.StopAdvertisingAsync();
        await advertiser.StopAdvertisingAsync(); // Should be no-op

        Assert.False(advertiser.IsAdvertising);

        // Cleanup
        advertiser.Dispose();
    }
}

sealed internal class TestAdvertisingOptions : IAdvertisingOptions
{
    public string ServiceName { get; set; } = "";
    public IDictionary<string, string> DiscoveryInfo { get; set; } = new Dictionary<string, string>();
}

#endif