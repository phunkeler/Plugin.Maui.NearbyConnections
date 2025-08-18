using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Tests;

public class AdvertisingTests
{
    [Fact]
    public async Task Advertise()
    {
        Console.WriteLine("[TEST] ===== STARTING ADVERTISE TEST =====");

        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertisingOptions
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
        advertiser.AdvertisingStateChanged += (s, e) =>
        {
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

        await Task.Delay(20000); // Give time for callbacks
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
        var discoverer = new Discoverer();
        var options = new DiscoveringOptions
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

        await Task.Delay(20000); // Give time for callbacks
        Console.WriteLine("[TEST] ===== DISCOVER TEST COMPLETED =====");
    }
}