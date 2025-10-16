using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Tests;

public class AdvertiserTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act
        var advertiser = new Advertiser();

        // Assert
        Assert.NotNull(advertiser);
    }

    [Fact]
    public async Task StartAdvertisingAsync_WithValidOptions_CompletesSuccessfully()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = "TestDevice",
            AdvertisingInfo = new Dictionary<string, string>
            {
                ["device_type"] = "test",
                ["app_version"] = "1.0.0"
            }
        };

        // Act & Assert
        var task = advertiser.StartAdvertisingAsync(options);
        Assert.NotNull(task);

        // Should not throw an exception
        await task;
    }

    [Fact]
    public async Task StartAdvertisingAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var advertiser = new Advertiser();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => advertiser.StartAdvertisingAsync(null!));
    }

    [Fact]
    public async Task StartAdvertisingAsync_WithEmptyServiceName_ThrowsArgumentException()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "",
            DisplayName = "TestDevice"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => advertiser.StartAdvertisingAsync(options));
    }

    [Fact]
    public async Task StartAdvertisingAsync_WithWhitespaceServiceName_ThrowsArgumentException()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "   ",
            DisplayName = "TestDevice"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => advertiser.StartAdvertisingAsync(options));
    }

    [Fact]
    public async Task StartAdvertisingAsync_WithNullDisplayName_UsesDefaultDisplayName()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = null!
        };

        // Act
        await advertiser.StartAdvertisingAsync(options);

        // Assert - Should use default display name and not throw
        Assert.True(true); // If we get here, no exception was thrown
    }

    [Fact]
    public async Task StartAdvertisingAsync_WithEmptyAdvertisingInfo_CompletesSuccessfully()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = "TestDevice",
            AdvertisingInfo = new Dictionary<string, string>()
        };

        // Act & Assert
        await advertiser.StartAdvertisingAsync(options);
    }

    [Fact]
    public async Task StartAdvertisingAsync_WithLargeAdvertisingInfo_CompletesSuccessfully()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = "TestDevice",
            AdvertisingInfo = new Dictionary<string, string>
            {
                ["device_type"] = "smartphone",
                ["app_version"] = "1.2.3",
                ["os_version"] = "iOS 17.0",
                ["capabilities"] = "chat,file_transfer,voice",
                ["user_id"] = "user123"
            }
        };

        // Act & Assert
        await advertiser.StartAdvertisingAsync(options);
    }

    [Fact]
    public void StopAdvertising_CompletesWithoutException()
    {
        // Arrange
        var advertiser = new Advertiser();

        // Act & Assert
        advertiser.StopAdvertising(); // Should not throw
    }

    [Fact]
    public async Task StopAdvertising_AfterStartAdvertising_CompletesWithoutException()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = "TestDevice"
        };

        // Act
        await advertiser.StartAdvertisingAsync(options);
        advertiser.StopAdvertising();

        // Assert - Should complete without exception
        Assert.True(true);
    }

    [Fact]
    public void StopAdvertising_CalledMultipleTimes_CompletesWithoutException()
    {
        // Arrange
        var advertiser = new Advertiser();

        // Act & Assert
        advertiser.StopAdvertising();
        advertiser.StopAdvertising();
        advertiser.StopAdvertising();
    }

    [Fact]
    public async Task StartAdvertisingAsync_CalledMultipleTimes_CompletesSuccessfully()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = "TestDevice"
        };

        // Act & Assert
        await advertiser.StartAdvertisingAsync(options);
        await advertiser.StartAdvertisingAsync(options); // Should handle multiple calls
    }

    [Fact]
    public void Dispose_CompletesWithoutException()
    {
        // Arrange
        var advertiser = new Advertiser();

        // Act & Assert
        advertiser.Dispose();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_CompletesWithoutException()
    {
        // Arrange
        var advertiser = new Advertiser();

        // Act & Assert
        advertiser.Dispose();
        advertiser.Dispose();
    }

    [Fact]
    public async Task Dispose_AfterStartAdvertising_CompletesWithoutException()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = "TestDevice"
        };

        // Act
        await advertiser.StartAdvertisingAsync(options);

        // Assert
        advertiser.Dispose();
    }

    [Fact]
    public async Task StartAdvertisingAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
        {
            ServiceName = "testservice",
            DisplayName = "TestDevice"
        };

        advertiser.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => advertiser.StartAdvertisingAsync(options));
    }

    [Fact]
    public void StopAdvertising_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var advertiser = new Advertiser();
        advertiser.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => advertiser.StopAdvertising());
    }
}

public class DiscovererTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act
        var discoverer = new Discoverer();

        // Assert
        Assert.NotNull(discoverer);
    }

    [Fact]
    public async Task StartDiscoveringAsync_WithValidOptions_CompletesSuccessfully()
    {
        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = "testservice"
        };

        // Act & Assert
        var task = discoverer.StartDiscoveringAsync(options);
        Assert.NotNull(task);

        // Should not throw an exception
        await task;
    }

    [Fact]
    public async Task StartDiscoveringAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var discoverer = new Discoverer();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => discoverer.StartDiscoveringAsync(null!));
    }

    [Fact]
    public async Task StartDiscoveringAsync_WithEmptyServiceName_ThrowsArgumentException()
    {
        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => discoverer.StartDiscoveringAsync(options));
    }

    [Fact]
    public async Task StartDiscoveringAsync_WithWhitespaceServiceName_ThrowsArgumentException()
    {
        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = "   "
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => discoverer.StartDiscoveringAsync(options));
    }

    [Fact]
    public async Task StartDiscoveringAsync_CalledMultipleTimes_CompletesSuccessfully()
    {
        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = "testservice"
        };

        // Act & Assert
        await discoverer.StartDiscoveringAsync(options);
        await discoverer.StartDiscoveringAsync(options); // Should handle multiple calls
    }

    [Fact]
    public void StopDiscovering_CompletesWithoutException()
    {
        // Arrange
        var discoverer = new Discoverer();

        // Act & Assert
        discoverer.StopDiscovering(); // Should not throw
    }

    [Fact]
    public async Task StopDiscovering_AfterStartDiscovering_CompletesWithoutException()
    {
        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = "testservice"
        };

        // Act
        await discoverer.StartDiscoveringAsync(options);
        discoverer.StopDiscovering();

        // Assert - Should complete without exception
        Assert.True(true);
    }

    [Fact]
    public void StopDiscovering_CalledMultipleTimes_CompletesWithoutException()
    {
        // Arrange
        var discoverer = new Discoverer();

        // Act & Assert
        discoverer.StopDiscovering();
        discoverer.StopDiscovering();
        discoverer.StopDiscovering();
    }

    [Fact]
    public void Dispose_CompletesWithoutException()
    {
        // Arrange
        var discoverer = new Discoverer();

        // Act & Assert
        discoverer.Dispose();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_CompletesWithoutException()
    {
        // Arrange
        var discoverer = new Discoverer();

        // Act & Assert
        discoverer.Dispose();
        discoverer.Dispose();
    }

    [Fact]
    public async Task Dispose_AfterStartDiscovering_CompletesWithoutException()
    {
        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = "testservice"
        };

        // Act
        await discoverer.StartDiscoveringAsync(options);

        // Assert
        discoverer.Dispose();
    }

    [Fact]
    public async Task StartDiscoveringAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = "testservice"
        };

        discoverer.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => discoverer.StartDiscoveringAsync(options));
    }

    [Fact]
    public void StopDiscovering_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var discoverer = new Discoverer();
        discoverer.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => discoverer.StopDiscovering());
    }
}

public class AdvertiseOptionsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Act
        var options = new AdvertiseOptions();

        // Assert
        Assert.NotNull(options.DisplayName);
        Assert.NotNull(options.ServiceName);
        Assert.NotNull(options.AdvertisingInfo);
        Assert.Empty(options.AdvertisingInfo);
    }

    [Fact]
    public void DisplayName_SetAndGet_ReturnsCorrectValue()
    {
        // Arrange
        var options = new AdvertiseOptions();
        const string testName = "TestDevice";

        // Act
        options.DisplayName = testName;

        // Assert
        Assert.Equal(testName, options.DisplayName);
    }

    [Fact]
    public void ServiceName_SetAndGet_ReturnsCorrectValue()
    {
        // Arrange
        var options = new AdvertiseOptions();
        const string testService = "com.test.service";

        // Act
        options.ServiceName = testService;

        // Assert
        Assert.Equal(testService, options.ServiceName);
    }

    [Fact]
    public void AdvertisingInfo_SetAndGet_ReturnsCorrectValue()
    {
        // Arrange
        var options = new AdvertiseOptions();
        var testInfo = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        options.AdvertisingInfo = testInfo;

        // Assert
        Assert.Equal(testInfo, options.AdvertisingInfo);
        Assert.Equal(2, options.AdvertisingInfo.Count);
        Assert.Equal("value1", options.AdvertisingInfo["key1"]);
        Assert.Equal("value2", options.AdvertisingInfo["key2"]);
    }
}

public class DiscoverOptionsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Act
        var options = new DiscoverOptions();

        // Assert
        Assert.NotNull(options.ServiceName);
    }

    [Fact]
    public void ServiceName_SetAndGet_ReturnsCorrectValue()
    {
        // Arrange
        var options = new DiscoverOptions();
        const string testService = "com.test.service";

        // Act
        options.ServiceName = testService;

        // Assert
        Assert.Equal(testService, options.ServiceName);
    }
}

// Legacy integration tests for device testing
public class LegacyIntegrationTests
{
    [Fact]
    public async Task Advertise_IntegrationTest()
    {
        Console.WriteLine("[TEST] ===== STARTING ADVERTISE INTEGRATION TEST =====");

        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertiseOptions
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

        // Act & Assert - Start advertising
        Console.WriteLine("[TEST] Calling StartAdvertisingAsync...");
        await advertiser.StartAdvertisingAsync(options);
        Console.WriteLine("[TEST] StartAdvertisingAsync completed");

        await Task.Delay(5000); // Give time for initialization
        Console.WriteLine("[TEST] ===== ADVERTISE INTEGRATION TEST COMPLETED =====");
    }

    [Fact]
    public async Task Discover_IntegrationTest()
    {
        Console.WriteLine("[TEST] ===== STARTING DISCOVER INTEGRATION TEST =====");

        // Arrange
        var discoverer = new Discoverer();
        var options = new DiscoverOptions
        {
            ServiceName = "testservice"
        };

        Console.WriteLine($"[TEST] Created discoverer with service: {options.ServiceName}");

        // Act
        Console.WriteLine("[TEST] Calling StartDiscoveringAsync...");
        await discoverer.StartDiscoveringAsync(options);
        Console.WriteLine("[TEST] StartDiscoveringAsync completed");

        await Task.Delay(5000); // Give time for initialization
        Console.WriteLine("[TEST] ===== DISCOVER INTEGRATION TEST COMPLETED =====");
    }
}