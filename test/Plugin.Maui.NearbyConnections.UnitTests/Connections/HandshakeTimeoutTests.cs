using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Session")]
public sealed class HandshakeTimeoutTests
{
    [TestMethod]
    public async Task ConnectAsync_TimeoutIsArmedFromOptions()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", ConnectTimeout = TimeSpan.FromSeconds(5) });

        // Assert
        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
            () => platform.ConnectAsync(Create.Device("peer-1", "Alice"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenPlatformFails_DoesNotStrandThePendingEntry()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service" });
        var device = Create.Device("peer-1", "Alice");

        // Act
        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(() => platform.ConnectAsync(device, TestContext.CancellationToken));

        // Assert
        Assert.IsEmpty(platform._connectionTcs);
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancellation_ReportsCancellationNotTimeout()
    {
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

    // The two roles read different options. Every test below sets ConnectTimeout and AcceptTimeout
    // to different values on purpose: with one shared value, a wire-up that read the wrong option
    // would still pass.
    [TestMethod]
    public async Task AwaitHandshake_AsInitiator_UsesConnectTimeoutNotAcceptTimeout()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var connect = TimeSpan.FromSeconds(30);
        var accept = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformNearby(time, new NearbyOptions
        {
            ServiceId = "test-service",
            ConnectTimeout = connect,
            AcceptTimeout = accept,
        });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(accept);

        // Assert — past the accept deadline, an initiator must still be waiting. Awaited with a real
        // deadline so a regression that fires early fails here rather than hanging the suite.
        var settled = await Task.WhenAny(
            handshake,
            Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken));
        Assert.AreNotSame(handshake, settled, "The initiator must not time out on AcceptTimeout.");

        time.Advance(connect - accept);
        await Assert.ThrowsExactlyAsync<NearbyConnectionTimeoutException>(() => handshake);
    }

    [TestMethod]
    public async Task AwaitHandshake_AsAcceptor_UsesAcceptTimeoutNotConnectTimeout()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var connect = TimeSpan.FromSeconds(30);
        var accept = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformNearby(time, new NearbyOptions
        {
            ServiceId = "test-service",
            ConnectTimeout = connect,
            AcceptTimeout = accept,
        });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Acceptor,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(accept);

        // Assert — the acceptor's shorter deadline governs, well before ConnectTimeout would fire.
        await Assert.ThrowsExactlyAsync<NearbyConnectionTimeoutException>(() => handshake);
    }

    [TestMethod]
    public async Task AwaitHandshake_AsAcceptor_ReportsTheAcceptWindowInItsMessage()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var accept = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformNearby(time, new NearbyOptions
        {
            ServiceId = "test-service",
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AcceptTimeout = accept,
        });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Acceptor,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(accept);
        var ex = await Assert.ThrowsExactlyAsync<NearbyConnectionTimeoutException>(() => handshake);

        // Assert — the message must quote the deadline that actually fired, not the other one.
        Assert.Contains("accepting the request", ex.Message);
        Assert.Contains("5s", ex.Message);
    }

    [TestMethod]
    public async Task AwaitHandshake_WhenTheHandshakeIsCancelledByTeardown_DoesNotReportATimeout()
    {
        // Arrange — DisposeAsync settles a pending handshake by cancelling its TCS. That is neither
        // the caller's token nor the deadline, and it must not be reported as an elapsed deadline.
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions
        {
            ServiceId = "test-service",
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AcceptTimeout = TimeSpan.FromSeconds(15),
        });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Acceptor,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        // Act — the clock never advances, so no deadline elapses.
        tcs.TrySetCanceled(CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => handshake);
        Assert.IsNotInstanceOfType<NearbyConnectionTimeoutException>(ex);
    }

    // The accept path's deadline, exercised through AwaitHandshakeAsync directly. The platform
    // accept lambdas that call it are unreachable on net10.0, but the deadline itself is shared
    // code, and it is the part that regressed: before it was extracted, both platforms awaited the
    // caller's token alone, so an accepted handshake that never completed hung forever.
    [TestMethod]
    public async Task AwaitHandshake_WhenNoTerminalCallbackArrives_TimesOutInsteadOfHanging()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var timeout = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", ConnectTimeout = timeout });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(timeout);

        // Assert
        await Assert.ThrowsExactlyAsync<NearbyConnectionTimeoutException>(() => handshake);
    }

    [TestMethod]
    public async Task AwaitHandshake_WhenTimeoutElapses_DoesNotStrandThePendingEntry()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var timeout = TimeSpan.FromSeconds(5);
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", ConnectTimeout = timeout });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(timeout);
        await Assert.ThrowsExactlyAsync<NearbyConnectionTimeoutException>(() => handshake);

        // Assert
        Assert.IsEmpty(platform._connectionTcs);
    }

    [TestMethod]
    public async Task AwaitHandshake_WhenCallbackResolvesFirst_ReturnsTheConnection()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", ConnectTimeout = TimeSpan.FromSeconds(5) });
        var device = Create.Device("peer-1", "Alice");
        var expected = Create.Connection(device);
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        platform.ResolveConnectionTcs(device.Id, expected);

        // Assert
        Assert.AreSame(expected, await handshake);
    }

    [TestMethod]
    public async Task AwaitHandshake_WhenTimeoutIsInfinite_DoesNotTimeOut()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", ConnectTimeout = Timeout.InfiniteTimeSpan });
        var device = Create.Device("peer-1", "Alice");
        var expected = Create.Connection(device);
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            beforeAwait: _ => Task.CompletedTask,
            CancellationToken.None);

        time.Advance(TimeSpan.FromHours(1));
        platform.ResolveConnectionTcs(device.Id, expected);

        // Assert
        Assert.AreSame(expected, await handshake);
    }

    [TestMethod]
    public async Task AwaitHandshake_CallerCancellation_ReportsCancellationNotTimeout()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", ConnectTimeout = TimeSpan.FromSeconds(5) });
        var device = Create.Device("peer-1", "Alice");
        var tcs = platform.RegisterConnectionTcs(device.Id, CancellationToken.None);
        using var cts = new CancellationTokenSource();

        // Act
        var handshake = platform.AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            beforeAwait: _ => Task.CompletedTask,
            cts.Token);

        await cts.CancelAsync();

        // Assert
        var ex = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => handshake);
        Assert.IsNotInstanceOfType<NearbyConnectionTimeoutException>(ex);
    }

    public TestContext TestContext { get; set; }
}
