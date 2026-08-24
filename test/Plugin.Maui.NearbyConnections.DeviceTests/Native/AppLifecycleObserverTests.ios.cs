using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// Wiring smoke for the background-teardown observer — the shipped-but-never-hardware-verified
/// path from the iOS backgrounding work. Exercises the real <c>NSNotificationCenter</c>
/// registration and callback marshaling on a live UIKit runtime.
/// </summary>
/// <remarks>
/// Assertion ceiling: the observer's only effect is <c>StopAsync</c> on an idle session, which has
/// no externally visible state change without a live radio. What these tests pin is that
/// registration, delivery, and double-dispose don't throw on a real runtime — the class of failure
/// a compile check cannot catch (selector/thread marshaling) — and that disposal waits for a
/// teardown already in flight rather than leaving it running.
/// </remarks>
public class AppLifecycleObserverTests : DeviceTest
{
    [Fact]
    public async Task BackgroundNotification_DeliveredToLiveObserver_NoThrow()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        var session = new NearbyImplementation(platform, new NearbyOptions { ServiceId = "devtest" }, NullLogger.Instance);
        var observer = new AppLifecycleObserver(session, NullLogger.Instance);

        // Act — the real notification the OS posts on backgrounding.
        // Posted on the main thread because that is where iOS posts it: UIKit's own observers
        // (keyboard, view layout) are subscribed to this notification and assume main-thread
        // delivery, so posting from the xUnit worker thread makes them log main-thread violations.
        await MainThread.InvokeOnMainThreadAsync(() =>
            NSNotificationCenter.DefaultCenter.PostNotificationName(
                UIApplication.DidEnterBackgroundNotification, null));

        // Settles the teardown the notification started: disposal awaits it, which is what
        // replaced the fixed delay this test used to need.
        await observer.DisposeAsync();

        // Assert — the session reports stopped states (it was never started; the point is the
        // delivery path executed without throwing).
        Assert.False(session.IsAdvertising);
        Assert.False(session.IsDiscovering);
    }

    [Fact]
    public async Task Dispose_IsIdempotentAndUnregisters()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        var session = new NearbyImplementation(platform, new NearbyOptions { ServiceId = "devtest" }, NullLogger.Instance);
        var observer = new AppLifecycleObserver(session, NullLogger.Instance);

        // Act — dispose twice, then post: a stale registration would invoke a disposed observer.
        // Main thread for the same reason as above.
        await observer.DisposeAsync();
        await observer.DisposeAsync();
        await MainThread.InvokeOnMainThreadAsync(() =>
            NSNotificationCenter.DefaultCenter.PostNotificationName(
                UIApplication.DidEnterBackgroundNotification, null));

        // Assert
        Assert.False(session.IsAdvertising);
    }

}
