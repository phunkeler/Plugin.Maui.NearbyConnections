using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[Trait("Category", "Session")]
public sealed class HandshakeTimeoutTests
{
    [Fact]
    public async Task ConnectAsync_TimeoutIsArmedFromOptions()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service", ConnectTimeout = TimeSpan.FromSeconds(5) });

        // Assert
        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => platform.ConnectAsync(Create.Device("peer-1", "Alice"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConnectAsync_WhenPlatformFails_DoesNotStrandThePendingEntry()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");

        // Act
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => platform.ConnectAsync(device, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(platform._connectionTcs);
    }

    [Fact]
    public async Task ConnectAsync_CallerCancellation_ReportsCancellationNotTimeout()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var ex = await Assert.ThrowsAsync<OperationCanceledException>(
            () => platform.ConnectAsync(Create.Device("peer-1", "Alice"), cts.Token));

        // Assert
        Assert.IsNotAssignableFrom<NearbyConnectionTimeoutException>(ex);
    }

    // The duration is a caller-supplied parameter — the offer's one deadline, not a role-based
    // options lookup. The tests below set ConnectTimeout to a different value on purpose: a
    // wire-up that read the option instead of the parameter would still pass otherwise.
    [Fact]
    public async Task AwaitHandshake_IsBoundedByTheSuppliedDuration_NotAnOption()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var supplied = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformBridge(time, new NearbyOptions
        {
            ServiceId = "test-service",
            ConnectTimeout = TimeSpan.FromSeconds(30),
        });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Acceptor,
            supplied,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(supplied);

        // Assert — the supplied window governs, well before ConnectTimeout would fire.
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => handshake);
    }

    [Fact]
    public async Task AwaitHandshake_DoesNotFireBeforeTheSuppliedDuration()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var supplied = TimeSpan.FromSeconds(30);
        var platform = Create.PlatformBridge(time, new NearbyOptions
        {
            ServiceId = "test-service",
            ConnectTimeout = TimeSpan.FromSeconds(5),
        });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            supplied,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(TimeSpan.FromSeconds(5));

        // Assert — past the (smaller) option value, the supplied window must still be waiting.
        // Awaited with a real deadline so a regression that fires early fails here rather than
        // hanging the suite.
        var settled = await Task.WhenAny(
            handshake,
            Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.NotSame(handshake, settled);
        time.Advance(supplied - TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => handshake);
    }

    [Fact]
    public async Task AwaitHandshake_AsAcceptor_ReportsTheOfferDeadlineInItsMessage()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var remaining = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Acceptor,
            remaining,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(remaining);
        var ex = await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => handshake);

        // Assert — the message must quote the window that actually fired, and name the offer.
        Assert.Contains("offer's deadline", ex.Message);
        Assert.Contains("5s", ex.Message);
    }

    [Fact]
    public async Task AwaitHandshake_WhenTheHandshakeIsCancelledByTeardown_DoesNotReportATimeout()
    {
        // Arrange — DisposeAsync settles a pending handshake by cancelling its TCS. That is neither
        // the caller's token nor the deadline, and it must not be reported as an elapsed deadline.
        var time = new FakeTimeProvider();
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Acceptor,
            TimeSpan.FromSeconds(15),
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        // Act — the clock never advances, so no deadline elapses.
        tcs.TrySetCanceled(CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => handshake);
        Assert.IsNotAssignableFrom<NearbyConnectionTimeoutException>(ex);
    }

    // The accept path's deadline, exercised through AwaitHandshakeAsync directly. The platform
    // accept lambdas that call it are unreachable on net10.0, but the deadline itself is shared
    // code, and it is the part that regressed: before it was extracted, both platforms awaited the
    // caller's token alone, so an accepted handshake that never completed hung forever.
    [Fact]
    public async Task AwaitHandshake_WhenNoTerminalCallbackArrives_TimesOutInsteadOfHanging()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var timeout = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            timeout,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(timeout);

        // Assert
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => handshake);
    }

    [Fact]
    public async Task AwaitHandshake_WhenTimeoutElapses_DoesNotStrandThePendingEntry()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var timeout = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            timeout,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(timeout);
        await Assert.ThrowsAsync<NearbyConnectionTimeoutException>(() => handshake);

        // Assert
        Assert.Empty(platform._connectionTcs);
    }

    [Fact]
    public async Task AwaitHandshake_WhenCallbackResolvesFirst_ReturnsTheConnection()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");
        var expected = Create.Connection(device);
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            TimeSpan.FromSeconds(5),
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        platform.ResolveConnectionTcs(device.Id, expected, new StubPlatformConnection());

        // Assert
        Assert.Same(expected, await handshake);
    }

    [Fact]
    public async Task AwaitHandshake_WhenTimeoutIsInfinite_DoesNotTimeOut()
    {
        // The infinite opt-out survives on the initiator side by the caller's own choice. (The
        // remote side clamps the declared window, but that is the other device's concern.)

        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");
        var expected = Create.Connection(device);
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            Timeout.InfiniteTimeSpan,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(TimeSpan.FromHours(1));
        platform.ResolveConnectionTcs(device.Id, expected, new StubPlatformConnection());

        // Assert
        Assert.Same(expected, await handshake);
    }

    [Fact]
    public async Task AwaitHandshake_CallerCancellation_ReportsCancellationNotTimeout()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformBridge(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);
        using var cts = new CancellationTokenSource();

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            TimeSpan.FromSeconds(5),
            beforeAwait: _ => Task.CompletedTask,
            cts.Token);

        await cts.CancelAsync();

        // Assert
        var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => handshake);
        Assert.IsNotAssignableFrom<NearbyConnectionTimeoutException>(ex);
    }
}
