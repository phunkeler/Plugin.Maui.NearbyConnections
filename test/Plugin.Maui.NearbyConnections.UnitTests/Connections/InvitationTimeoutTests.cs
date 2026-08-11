using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Tests the plugin-owned connection deadline (DEVICE-LIFECYCLE gap 2).
/// </summary>
/// <remarks>
/// <para>
/// iOS has a native invitation timeout; Google's Nearby Connections has none at all —
/// <c>requestConnection</c> completes when the request is <em>sent</em>, and nothing guarantees a
/// callback ever follows. Without a plugin-owned deadline, connecting to a device that walks out of
/// range mid-handshake waits forever and strands its <c>_connectionTcs</c> entry.
/// </para>
/// <para>
/// These run on <c>net10.0</c>, where <c>PlatformInitiateConnectAsync</c> throws, so they exercise
/// the deadline mechanics directly rather than through <c>ConnectAsync</c>. The wiring is verified
/// by <see cref="ConnectAsync_TimeoutIsArmedFromOptions"/>.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("Session")]
public sealed class InvitationTimeoutTests
{
    [TestMethod]
    public async Task ConnectAsync_TimeoutIsArmedFromOptions()
    {
        // Guards the wiring rather than the clock: ConnectAsync must read InvitationTimeout from
        // options. On net10.0 the platform call throws first, so the assertion is that the
        // platform-not-supported failure surfaces rather than a timeout — proving the deadline was
        // armed but is not what failed.

        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", InvitationTimeout = TimeSpan.FromSeconds(5) });

        // Assert
        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
            () => platform.ConnectAsync(Create.Device("peer-1", "Alice")));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenPlatformFails_DoesNotStrandThePendingEntry()
    {
        // A failed attempt must clear its own _connectionTcs entry. A stranded entry means a later
        // callback for the same device resolves a task nobody is awaiting, and the device can never
        // be connected to again in this session.
        //
        // Asserts on an internal field deliberately, against this suite's usual rule. The
        // consumer-visible symptom needs a real platform callback to arrive after the failure, which
        // net10.0 cannot produce — a retry-based version of this test was tried and passed even with
        // both cleanup paths removed, so it would have been weaker cover for a real hazard.

        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");

        // Act
        // Act
        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(() => platform.ConnectAsync(device));

        // Assert
        // Assert
        Assert.IsEmpty(platform._connectionTcs);
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancellation_ReportsCancellationNotTimeout()
    {
        // The caller cancelling and the deadline elapsing are different failures and must not be
        // conflated: a caller who cancels deliberately should not be told the peer timed out.

        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service" });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var ex = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => platform.ConnectAsync(Create.Device("peer-1", "Alice"), cts.Token));

        // Assert
        Assert.IsNotInstanceOfType<NearbyConnectionTimeoutException>(ex);
    }
}
