#if IOS
using Xunit;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Tests;

public class AdvertisingTests
{
    [Fact]
    public async Task Advertise()
    {
        Console.WriteLine("[TEST] ===== STARTING ADVERTISE TEST =====");
        
        // Arrange
        var advertiser = new NearbyConnectionsAdvertiser();
        var options = new TestAdvertisingOptions
        {
            ServiceName = "testservice",
            AdvertisingInfo = new Dictionary<string, string>
            {
                ["device_type"] = "test",
                ["app_version"] = "1.0.0"
            }
        };

        Console.WriteLine($"[TEST] Created advertiser with service: {options.ServiceName}");
        Console.WriteLine($"[TEST] Advertising info count: {options.AdvertisingInfo.Count}");

        bool stateChanged = false;
        advertiser.AdvertisingStateChanged += (s, e) => {
            Console.WriteLine($"[TEST] AdvertisingStateChanged event fired: {e}");
            stateChanged = true;
        };

        // Act & Assert - Start advertising
        Console.WriteLine($"[TEST] Initial IsAdvertising state: {advertiser.IsAdvertising}");
        Assert.False(advertiser.IsAdvertising);

        Console.WriteLine("[TEST] Calling StartAdvertisingAsync...");
        await advertiser.StartAdvertisingAsync(options);
        
        Console.WriteLine($"[TEST] After StartAdvertisingAsync, IsAdvertising: {advertiser.IsAdvertising}");
        Console.WriteLine($"[TEST] StateChanged event fired: {stateChanged}");
        
        Assert.True(advertiser.IsAdvertising);
        Assert.True(stateChanged);
        
        Console.WriteLine("[TEST] ===== ADVERTISE TEST COMPLETED =====");
    }
}

public class DiscoveringTests
{
    [Fact]
    public async Task Discover()
    {
        Console.WriteLine("[TEST] ===== STARTING DISCOVER TEST =====");
        
        // Arrange
        var discoverer = new NearbyConnectionsDiscoverer();
        var options = new TestDiscoveringOptions
        {
            ServiceName = "testservice"
        };

        Console.WriteLine($"[TEST] Created discoverer with service: {options.ServiceName}");
        Console.WriteLine($"[TEST] Initial IsDiscovering state: {discoverer.IsDiscovering}");

        // Act
        Console.WriteLine("[TEST] Calling StartDiscoveringAsync...");
        await discoverer.StartDiscoveringAsync(options);

        // Assert
        Console.WriteLine($"[TEST] After StartDiscoveringAsync, IsDiscovering: {discoverer.IsDiscovering}");
        Assert.True(discoverer.IsDiscovering);
        
        Console.WriteLine("[TEST] ===== DISCOVER TEST COMPLETED =====");
    }
}

sealed internal class TestAdvertisingOptions : IAdvertisingOptions
{
    public string ServiceName { get; set; } = "";
    public IDictionary<string, string> AdvertisingInfo { get; set; } = new Dictionary<string, string>();
}

sealed internal class TestDiscoveringOptions : IDiscoveringOptions
{
    public string ServiceName { get; set; } = "";
}

#endif