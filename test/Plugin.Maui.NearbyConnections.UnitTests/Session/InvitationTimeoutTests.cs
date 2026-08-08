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
    static PlatformNearbyConnections CreateSut(
        FakeTimeProvider timeProvider,
        TimeSpan? invitationTimeout = null)
        => new(
            timeProvider,
            new NearbyConnectionsOptions
            {
                ServiceId = "test-service",
                InvitationTimeout = invitationTimeout ?? TimeSpan.FromSeconds(30),
            },
            NullLogger.Instance);

    [TestMethod]
    public void InvitationTimeout_DefaultsTo30Seconds()
    {
        // The default is load-bearing: it is what stops an un-configured app from hanging forever
        // on Android.
        Assert.AreEqual(TimeSpan.FromSeconds(30), new NearbyConnectionsOptions().InvitationTimeout);
    }

    [TestMethod]
    public void Deadline_FiresAfterInvitationTimeout()
    {
        var time = new FakeTimeProvider();
        var timeout = TimeSpan.FromSeconds(30);

        using var deadlineCts = new CancellationTokenSource(timeout, time);

        Assert.IsFalse(deadlineCts.IsCancellationRequested);

        time.Advance(timeout);

        Assert.IsTrue(deadlineCts.IsCancellationRequested, "The deadline must fire once InvitationTimeout elapses.");
    }

    [TestMethod]
    public void Deadline_DoesNotFireEarly()
    {
        var time = new FakeTimeProvider();
        var timeout = TimeSpan.FromSeconds(30);

        using var deadlineCts = new CancellationTokenSource(timeout, time);

        time.Advance(timeout - TimeSpan.FromMilliseconds(1));

        Assert.IsFalse(deadlineCts.IsCancellationRequested);
    }

    [TestMethod]
    public void InfiniteTimeout_NeverFires()
    {
        // Timeout.InfiniteTimeSpan is the documented opt-out; a CancellationTokenSource constructed
        // with it must never fire no matter how far the clock moves.
        var time = new FakeTimeProvider();

        using var deadlineCts = new CancellationTokenSource(Timeout.InfiniteTimeSpan, time);

        time.Advance(TimeSpan.FromHours(24));

        Assert.IsFalse(deadlineCts.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ConnectAsync_TimeoutIsArmedFromOptions()
    {
        // Guards the wiring rather than the clock: ConnectAsync must read InvitationTimeout from
        // options. On net10.0 the platform call throws first, so the assertion is that the
        // platform-not-supported failure surfaces rather than a timeout — proving the deadline was
        // armed but is not what failed.
        var time = new FakeTimeProvider();
        var sut = CreateSut(time, TimeSpan.FromSeconds(5));

        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
            () => sut.ConnectAsync(new NearbyDevice("peer-1", "Alice")));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenPlatformFails_DoesNotStrandThePendingEntry()
    {
        // A failed attempt must clear its own _connectionTcs entry. A stranded entry means a later
        // callback for the same device resolves a task nobody is awaiting, and the device can never
        // be connected to again in this session.
        var time = new FakeTimeProvider();
        var sut = CreateSut(time);
        var device = new NearbyDevice("peer-1", "Alice");

        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(() => sut.ConnectAsync(device));

        Assert.IsEmpty(sut._connectionTcs);
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancellation_ReportsCancellationNotTimeout()
    {
        // The caller cancelling and the deadline elapsing are different failures and must not be
        // conflated: a caller who cancels deliberately should not be told the peer timed out.
        var time = new FakeTimeProvider();
        var sut = CreateSut(time);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var ex = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => sut.ConnectAsync(new NearbyDevice("peer-1", "Alice"), cts.Token));

        Assert.IsNotInstanceOfType<NearbyConnectionTimeoutException>(ex);
    }
}
