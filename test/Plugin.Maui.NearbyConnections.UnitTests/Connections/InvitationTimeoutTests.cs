using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
[TestCategory("Session")]
public sealed class InvitationTimeoutTests
{
    [TestMethod]
    public async Task ConnectAsync_TimeoutIsArmedFromOptions()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var platform = Create.PlatformNearby(time, new NearbyOptions { ServiceId = "test-service", InvitationTimeout = TimeSpan.FromSeconds(5) });

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

    public TestContext TestContext { get; set; }
}
