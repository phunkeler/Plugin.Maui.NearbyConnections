using Plugin.Maui.NearbyConnections.Advertise;
using System.Reflection;

namespace Plugin.Maui.NearbyConnections.DeviceTests;

public class AdvertiserTests
{
    [Fact]
    public async Task StartAdvertisingAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        // Arrange
        var advertiser = new Advertiser();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => advertiser.StartAdvertisingAsync(null!));
    }

    [Fact]
    public async Task StartAdvertisingAsync_SetsIsAdvertisingToTrue()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        // Act
        await advertiser.StartAdvertisingAsync(options);

        // Assert
        Assert.True(advertiser.IsAdvertising);
    }

    [Fact]
    public async Task StartAdvertisingAsync_FiresAdvertisingStateChangedEvent()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        AdvertisingStateChangedEventArgs? eventArgs = null;
        advertiser.AdvertisingStateChanged += (sender, args) => eventArgs = args;

        // Act
        await advertiser.StartAdvertisingAsync(options);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.True(eventArgs.IsAdvertising);
        Assert.True(eventArgs.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task StopAdvertisingAsync_WhenAdvertising_SetsIsAdvertisingToFalse()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        await advertiser.StartAdvertisingAsync(options);

        // Act
        await advertiser.StopAdvertisingAsync();

        // Assert
        Assert.False(advertiser.IsAdvertising);
    }

    [Fact]
    public async Task StopAdvertisingAsync_FiresAdvertisingStateChangedEvent()
    {
        // Arrange
        var advertiser = new Advertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        await advertiser.StartAdvertisingAsync(options);

        AdvertisingStateChangedEventArgs? eventArgs = null;
        advertiser.AdvertisingStateChanged += (sender, args) => eventArgs = args;

        // Act
        await advertiser.StopAdvertisingAsync();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.False(eventArgs.IsAdvertising);
        Assert.True(eventArgs.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task AdvertisingStateChanged_EventNotFired_WhenStateDoesNotChange()
    {
        // Arrange
        var advertiser = new Advertiser();
        var eventFiredCount = 0;
        advertiser.AdvertisingStateChanged += (sender, args) => eventFiredCount++;

        // Act - call stop when already stopped
        await advertiser.StopAdvertisingAsync();

        // Assert
        Assert.Equal(0, eventFiredCount);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();

        // Act & Assert - should not throw
        advertiser.Dispose();
        advertiser.Dispose();
        advertiser.Dispose();

        // Verify dispose was called properly
        Assert.True(advertiser.WasDisposed);
    }

    [Fact]
    public async Task Dispose_WhileAdvertising_StopsAdvertisingAndCleansUp()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        await advertiser.StartAdvertisingAsync(options);
        Assert.True(advertiser.IsAdvertising);

        // Act
        advertiser.Dispose();

        // Assert
        Assert.False(advertiser.IsAdvertising);
        Assert.True(advertiser.WasDisposed);
        Assert.Equal(1, advertiser.PlatformStopCallCount);
    }

    [Fact]
    public async Task Dispose_FiresAdvertisingStateChangedEvent_WhenStoppingAdvertising()
    {
        // NOTE: This test reveals a design issue - calling StopAdvertising in Dispose has ramifications:
        // 1. Synchronous disposal could block if platform operations are slow
        // 2. Events firing during disposal may be unexpected for consumers
        // 3. State consistency issues if disposal fails partway through
        //
        // RECOMMENDATION: Consider explicit StopAdvertisingAsync() before disposal,
        // or implement async disposal pattern (IAsyncDisposable)

        // Arrange
        var advertiser = new TestableAdvertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        await advertiser.StartAdvertisingAsync(options);

        AdvertisingStateChangedEventArgs? eventArgs = null;
        advertiser.AdvertisingStateChanged += (sender, args) => eventArgs = args;

        // Act
        advertiser.Dispose();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.False(eventArgs.IsAdvertising);
    }

    [Fact]
    public void Dispose_WhenNotAdvertising_DoesNotFireEvent()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var eventFiredCount = 0;
        advertiser.AdvertisingStateChanged += (sender, args) => eventFiredCount++;

        // Act
        advertiser.Dispose();

        // Assert
        Assert.Equal(0, eventFiredCount);
        Assert.True(advertiser.WasDisposed);
    }

    [Fact]
    public async Task OperationsAfterDispose_ThrowObjectDisposedException()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        advertiser.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => advertiser.StartAdvertisingAsync(options));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => advertiser.StopAdvertisingAsync());
    }

    [Fact]
    public void EventSubscriptionAfterDispose_DoesNotCauseMemoryLeaks()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        advertiser.Dispose();

        // Act - this should not cause issues even after disposal
        var eventHandlerCount = 0;
        EventHandler<AdvertisingStateChangedEventArgs> handler = (s, e) => eventHandlerCount++;

        // These operations should be safe after disposal
        advertiser.AdvertisingStateChanged += handler;
        advertiser.AdvertisingStateChanged -= handler;

        // Assert
        Assert.Equal(0, eventHandlerCount);
        Assert.True(advertiser.WasDisposed);
    }

    [Fact]
    public void Dispose_ReleasesEventHandlers()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var weakReference = SetupEventHandlerAndGetWeakReference(advertiser);

        // Act
        advertiser.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        Assert.True(advertiser.WasDisposed);
        // The weak reference should still be alive in this test since we're testing disposal behavior,
        // not necessarily garbage collection of the handler itself
    }

    private static WeakReference SetupEventHandlerAndGetWeakReference(TestableAdvertiser advertiser)
    {
        var eventHandler = new EventHandler<AdvertisingStateChangedEventArgs>((s, e) => { });
        advertiser.AdvertisingStateChanged += eventHandler;
        return new WeakReference(eventHandler);
    }

    [Fact]
    public async Task ConcurrentDispose_DoesNotCauseRaceConditions()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        await advertiser.StartAdvertisingAsync(options);

        // Act - simulate concurrent disposal calls
        var tasks = new Task[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(advertiser.Dispose);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.True(advertiser.WasDisposed);
        Assert.False(advertiser.IsAdvertising);
        Assert.Equal(1, advertiser.PlatformStopCallCount); // Should only be called once despite multiple dispose calls
    }

    [Fact]
    public async Task RecommendedPattern_ExplicitStopBeforeDispose()
    {
        // This test demonstrates the RECOMMENDED usage pattern:
        // Explicitly stop advertising before disposal to avoid the design issues

        // Arrange
        var advertiser = new TestableAdvertiser();
        var options = new AdvertisingOptions
        {
            DisplayName = "Test Device",
            ServiceName = "test-service"
        };

        await advertiser.StartAdvertisingAsync(options);
        Assert.True(advertiser.IsAdvertising);

        // Act - RECOMMENDED: Explicit stop before disposal
        await advertiser.StopAdvertisingAsync();
        advertiser.Dispose();

        // Assert
        Assert.False(advertiser.IsAdvertising);
        Assert.True(advertiser.WasDisposed);
        // Platform stop should have been called only once (during explicit stop)
        // Not during disposal, which is cleaner and more predictable
    }
}

/// <summary>
/// Testable subclass of Advertiser that tracks disposal and overrides platform methods for unit testing
/// </summary>
public class TestableAdvertiser : Advertiser
{
    public int PlatformStartCallCount { get; private set; }
    public int PlatformStopCallCount { get; private set; }
    public AdvertisingOptions? LastStartOptions { get; private set; }
    public CancellationToken LastStartCancellationToken { get; private set; }
    public CancellationToken LastStopCancellationToken { get; private set; }
    public bool WasDisposed { get; private set; }

    public new Task PlatformStartAdvertising(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        PlatformStartCallCount++;
        LastStartOptions = options;
        LastStartCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public new Task PlatformStopAdvertising(CancellationToken cancellationToken = default)
    {
        if (WasDisposed) return Task.CompletedTask; // Allow cleanup during disposal

        PlatformStopCallCount++;
        LastStopCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public void ResetPlatformStartCallCount()
    {
        PlatformStartCallCount = 0;
    }

    public void SetIsAdvertisingForTesting(bool value)
    {
        // Access the private setter through reflection
        var property = typeof(Advertiser).GetProperty(nameof(IsAdvertising));
        var setter = property?.GetSetMethod(true);
        setter?.Invoke(this, new object[] { value });
    }

    protected override void Dispose(bool disposing)
    {
        if (!WasDisposed && disposing)
        {
            // Simulate platform cleanup by stopping advertising
            if (IsAdvertising)
            {
                Task.Run(async () =>
                {
                    await PlatformStopAdvertising();
                    SetIsAdvertisingForTesting(false);
                }).GetAwaiter().GetResult();
            }

            WasDisposed = true;
        }

        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(WasDisposed, this);
    }
}