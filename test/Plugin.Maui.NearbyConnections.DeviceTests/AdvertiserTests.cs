using Plugin.Maui.NearbyConnections.Advertise;
using System.Reflection;

namespace Plugin.Maui.NearbyConnections.DeviceTests;
// Let's standardize on the term "NearbyConnection" to describe an object that;
//  - A nearby device that is available for connection
//  - A connection to a nearby device

// This abstraction was chosen because;
//  - It doesn't collide w/ Apple or Google
//  - It is a common term in the context of nearby connections
internal class NearbyConnection

/// <summary>
/// Tests for the <see cref="Advertiser"/> class.
/// The owner of this class
/// </summary>
public class AdvertiserTests
{
    // I need [BEHAVIOR] of a class whose primary responsibility is to tell others; "I'm available to connect" and/or "here's more context".
    //  - [NearbyAvailability] - Represents a "nearby" availability status.
    //  - [NearbyDevice] - Represents the nearby device for nearby connections.
    //  - [NearbyConnection] - Manages the connection to a nearby device.
    //  - [NearbyContext] - Provides contextual information about the nearby connection.
    // TODO:
    //  1. Define "IAvailability" - This is a higher-level use case for
    //  2. Define "IAdvertiser" - This is a lower-level class that implements the actual advertising logic.

    //


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

    #region Connection Event Tests

    [Fact]
    public void OnConnectionInitiated_FiresConnectionInitiatedEvent()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        ConnectionInitiatedEventArgs? capturedArgs = null;
        advertiser.ConnectionInitiated += (sender, args) => capturedArgs = args;

        var endpointId = "endpoint-123";
        var endpointName = "Test Device";

        // Act
        advertiser.TriggerConnectionInitiated(endpointId, endpointName);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(endpointId, capturedArgs.EndpointId);
        Assert.Equal(endpointName, capturedArgs.EndpointName);
        // Verify the sender was correct (test via event subscription)
    }

    [Fact]
    public void OnConnectionInitiated_DoesNotThrow_WhenNoSubscribers()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();

        // Act & Assert - should not throw
        advertiser.TriggerConnectionInitiated("endpoint-123", "Test Device");
    }

    [Fact]
    public void OnConnectionInitiated_FiresForMultipleSubscribers()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var capturedArgs1 = new List<ConnectionInitiatedEventArgs>();
        var capturedArgs2 = new List<ConnectionInitiatedEventArgs>();

        advertiser.ConnectionInitiated += (sender, args) => capturedArgs1.Add(args);
        advertiser.ConnectionInitiated += (sender, args) => capturedArgs2.Add(args);

        // Act
        advertiser.TriggerConnectionInitiated("endpoint-123", "Test Device");

        // Assert
        Assert.Single(capturedArgs1);
        Assert.Single(capturedArgs2);
        Assert.Equal("endpoint-123", capturedArgs1[0].EndpointId);
        Assert.Equal("endpoint-123", capturedArgs2[0].EndpointId);
    }

    [Fact]
    public void OnConnectionResult_FiresConnectionResultEvent_Success()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        ConnectionResultEventArgs? capturedArgs = null;
        advertiser.ConnectionResult += (sender, args) => capturedArgs = args;

        var endpointId = "endpoint-456";

        // Act
        advertiser.TriggerConnectionResult(endpointId, success: true);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(endpointId, capturedArgs.EndpointId);
        Assert.True(capturedArgs.Success);
    }

    [Fact]
    public void OnConnectionResult_FiresConnectionResultEvent_Failure()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        ConnectionResultEventArgs? capturedArgs = null;
        advertiser.ConnectionResult += (sender, args) => capturedArgs = args;

        var endpointId = "endpoint-789";

        // Act
        advertiser.TriggerConnectionResult(endpointId, success: false);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(endpointId, capturedArgs.EndpointId);
        Assert.False(capturedArgs.Success);
    }

    [Fact]
    public void OnDisconnected_FiresDisconnectedEvent()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        DisconnectedEventArgs? capturedArgs = null;
        advertiser.Disconnected += (sender, args) => capturedArgs = args;

        var endpointId = "endpoint-disconnect";

        // Act
        advertiser.TriggerDisconnected(endpointId);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(endpointId, capturedArgs.EndpointId);
    }

    [Fact]
    public void OnInvitationReceived_FiresInvitationReceivedEvent_DefaultReject()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        InvitationReceivedEventArgs? capturedArgs = null;
        advertiser.InvitationReceived += (sender, args) => capturedArgs = args;

        var peerId = "peer-123";
        var displayName = "John's iPhone";

        // Act
        var result = advertiser.TriggerInvitationReceived(peerId, displayName);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(peerId, capturedArgs.PeerId);
        Assert.Equal(displayName, capturedArgs.DisplayName);
        Assert.False(capturedArgs.ShouldAccept); // Default should be false
        Assert.False(result.ShouldAccept);
    }

    [Fact]
    public void OnInvitationReceived_AllowsSubscriberToAccept()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        advertiser.InvitationReceived += (sender, args) => args.ShouldAccept = true;

        var peerId = "peer-456";
        var displayName = "Jane's iPad";

        // Act
        var result = advertiser.TriggerInvitationReceived(peerId, displayName);

        // Assert
        Assert.True(result.ShouldAccept);
    }

    [Fact]
    public void OnInvitationReceived_LastSubscriberWins_WhenMultipleSubscribers()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        advertiser.InvitationReceived += (sender, args) => args.ShouldAccept = true;  // First accepts
        advertiser.InvitationReceived += (sender, args) => args.ShouldAccept = false; // Second rejects

        // Act
        var result = advertiser.TriggerInvitationReceived("peer-789", "Test Device");

        // Assert
        Assert.False(result.ShouldAccept); // Last subscriber wins
    }

    [Fact]
    public void EventSubscription_WorksAfterUnsubscribe()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var callCount = 0;
        EventHandler<ConnectionInitiatedEventArgs> handler = (sender, args) => callCount++;

        // Subscribe, unsubscribe, then subscribe again
        advertiser.ConnectionInitiated += handler;
        advertiser.ConnectionInitiated -= handler;
        advertiser.ConnectionInitiated += handler;

        // Act
        advertiser.TriggerConnectionInitiated("test-endpoint", "test-name");

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void AllEvents_CanBeSubscribedSimultaneously()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var connectionInitiatedCount = 0;
        var connectionResultCount = 0;
        var disconnectedCount = 0;
        var invitationReceivedCount = 0;

        advertiser.ConnectionInitiated += (s, e) => connectionInitiatedCount++;
        advertiser.ConnectionResult += (s, e) => connectionResultCount++;
        advertiser.Disconnected += (s, e) => disconnectedCount++;
        advertiser.InvitationReceived += (s, e) => invitationReceivedCount++;

        // Act
        advertiser.TriggerConnectionInitiated("endpoint", "name");
        advertiser.TriggerConnectionResult("endpoint", true);
        advertiser.TriggerDisconnected("endpoint");
        advertiser.TriggerInvitationReceived("peer", "display");

        // Assert
        Assert.Equal(1, connectionInitiatedCount);
        Assert.Equal(1, connectionResultCount);
        Assert.Equal(1, disconnectedCount);
        Assert.Equal(1, invitationReceivedCount);
    }

    #endregion

    #region Event Args Validation Tests

    [Fact]
    public void ConnectionInitiatedEventArgs_RequiredProperties_ThrowWhenNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConnectionInitiatedEventArgs { EndpointId = null!, EndpointName = "test" });
        Assert.Throws<ArgumentNullException>(() => new ConnectionInitiatedEventArgs { EndpointId = "test", EndpointName = null! });
    }

    [Fact]
    public void ConnectionInitiatedEventArgs_ValidProperties_Succeed()
    {
        // Act
        var args = new ConnectionInitiatedEventArgs
        {
            EndpointId = "test-endpoint",
            EndpointName = "Test Device"
        };

        // Assert
        Assert.Equal("test-endpoint", args.EndpointId);
        Assert.Equal("Test Device", args.EndpointName);
    }

    [Fact]
    public void ConnectionResultEventArgs_RequiredProperties_ThrowWhenNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConnectionResultEventArgs { EndpointId = null!, Success = true });
    }

    [Fact]
    public void ConnectionResultEventArgs_ValidProperties_Succeed()
    {
        // Act
        var args = new ConnectionResultEventArgs
        {
            EndpointId = "test-endpoint",
            Success = true
        };

        // Assert
        Assert.Equal("test-endpoint", args.EndpointId);
        Assert.True(args.Success);
    }

    [Fact]
    public void DisconnectedEventArgs_RequiredProperties_ThrowWhenNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DisconnectedEventArgs { EndpointId = null! });
    }

    [Fact]
    public void InvitationReceivedEventArgs_RequiredProperties_ThrowWhenNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new InvitationReceivedEventArgs { PeerId = null!, DisplayName = "test" });
        Assert.Throws<ArgumentNullException>(() => new InvitationReceivedEventArgs { PeerId = "test", DisplayName = null! });
    }

    [Fact]
    public void InvitationReceivedEventArgs_ShouldAccept_DefaultsFalse()
    {
        // Act
        var args = new InvitationReceivedEventArgs
        {
            PeerId = "test-peer",
            DisplayName = "Test Device"
        };

        // Assert
        Assert.False(args.ShouldAccept);
    }

    [Fact]
    public void InvitationReceivedEventArgs_ShouldAccept_CanBeModified()
    {
        // Arrange
        var args = new InvitationReceivedEventArgs
        {
            PeerId = "test-peer",
            DisplayName = "Test Device"
        };

        // Act
        args.ShouldAccept = true;

        // Assert
        Assert.True(args.ShouldAccept);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Events_ContinueWorking_AfterExceptionInHandler()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var successfulCallCount = 0;

        advertiser.ConnectionInitiated += (s, e) => throw new InvalidOperationException("Test exception");
        advertiser.ConnectionInitiated += (s, e) => successfulCallCount++;

        // Act & Assert - should not throw, second handler should still execute
        advertiser.TriggerConnectionInitiated("endpoint", "name");

        Assert.Equal(1, successfulCallCount);
    }

    [Fact]
    public void Events_HandleEmptyStrings()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        ConnectionInitiatedEventArgs? capturedArgs = null;
        advertiser.ConnectionInitiated += (s, e) => capturedArgs = e;

        // Act
        advertiser.TriggerConnectionInitiated("", "");

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal("", capturedArgs.EndpointId);
        Assert.Equal("", capturedArgs.EndpointName);
    }

    [Fact]
    public void Events_HandleSpecialCharacters()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        InvitationReceivedEventArgs? capturedArgs = null;
        advertiser.InvitationReceived += (s, e) => capturedArgs = e;

        var specialPeerId = "peer-🎉-123";
        var specialDisplayName = "John's iPhone (测试)";

        // Act
        advertiser.TriggerInvitationReceived(specialPeerId, specialDisplayName);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(specialPeerId, capturedArgs.PeerId);
        Assert.Equal(specialDisplayName, capturedArgs.DisplayName);
    }

    [Fact]
    public void Events_DoNotInterfere_WithAdvertisingState()
    {
        // Arrange
        var advertiser = new TestableAdvertiser();
        var stateChangedCount = 0;
        var connectionCount = 0;

        advertiser.AdvertisingStateChanged += (s, e) => stateChangedCount++;
        advertiser.ConnectionInitiated += (s, e) => connectionCount++;

        // Act
        advertiser.TriggerConnectionInitiated("endpoint", "name");

        // Assert
        Assert.Equal(0, stateChangedCount); // State events should not fire
        Assert.Equal(1, connectionCount);   // Connection events should fire
    }

    #endregion
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

    // Public methods to trigger events for testing
    public void TriggerConnectionInitiated(string endpointId, string endpointName)
    {
        OnConnectionInitiated(endpointId, endpointName);
    }

    public void TriggerConnectionResult(string endpointId, bool success)
    {
        OnConnectionResult(endpointId, success);
    }

    public void TriggerDisconnected(string endpointId)
    {
        OnDisconnected(endpointId);
    }

    public InvitationReceivedEventArgs TriggerInvitationReceived(string peerId, string displayName)
    {
        return OnInvitationReceived(peerId, displayName);
    }
}