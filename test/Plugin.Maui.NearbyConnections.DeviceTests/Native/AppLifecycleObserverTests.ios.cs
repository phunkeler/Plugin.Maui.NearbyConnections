using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Wiring smoke for the background-teardown observer — the shipped-but-never-hardware-verified
/// path from the iOS backgrounding work. Exercises the real <c>NSNotificationCenter</c>
/// registration and callback marshaling on a live UIKit runtime.
/// </summary>
/// <remarks>
/// Assertion ceiling: the observer's only effect is fire-and-forget <c>StopAsync</c> on an idle
/// session, which has no externally visible state change without a live radio. What this test
/// pins is that registration, delivery, and double-dispose don't throw on a real runtime — the
/// class of failure a compile check cannot catch (selector/thread marshaling).
/// </remarks>
public class AppLifecycleObserverTests
{
    [Fact]
    public async Task BackgroundNotification_DeliveredToLiveObserver_NoThrow()
    {
        // Arrange
        var platform = Create.PlatformNearby();
        var session = new NearbyImplementation(platform, new NearbyOptions { ServiceId = "devtest" }, NullLogger.Instance);
        using var observer = new AppLifecycleObserver(session, NullLogger.Instance);

        // Act — the real notification the OS posts on backgrounding.
        NSNotificationCenter.DefaultCenter.PostNotificationName(
            UIApplication.DidEnterBackgroundNotification, null);

        // Give the fire-and-forget teardown a beat to run on this runtime.
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert — the session reports stopped states (it was never started; the point is the
        // delivery path executed without throwing).
        Assert.False(session.IsAdvertising);
        Assert.False(session.IsDiscovering);
    }

    [Fact]
    public void Dispose_IsIdempotentAndUnregisters()
    {
        // Arrange
        var platform = Create.PlatformNearby();
        var session = new NearbyImplementation(platform, new NearbyOptions { ServiceId = "devtest" }, NullLogger.Instance);
        var observer = new AppLifecycleObserver(session, NullLogger.Instance);

        // Act — dispose twice, then post: a stale registration would invoke a disposed observer.
        observer.Dispose();
        observer.Dispose();
        NSNotificationCenter.DefaultCenter.PostNotificationName(
            UIApplication.DidEnterBackgroundNotification, null);

        // Assert
        Assert.False(session.IsAdvertising);
    }
}
