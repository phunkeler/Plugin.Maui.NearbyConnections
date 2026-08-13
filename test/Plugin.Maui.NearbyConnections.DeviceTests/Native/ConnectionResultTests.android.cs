namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The terminal "connection succeeded/failed" callback on Android (<c>OnConnectionResult</c>). The
/// repo's documented invariant: every failure path must resolve or fault the pending
/// <c>_connectionTcs</c> entry, or <c>AcceptAsync</c>/<c>ConnectAsync</c> hang forever.
/// </summary>
public class ConnectionResultTests
{
    [Fact]
    public async Task Success_ResolvesConnectionTcsAndRegistersActiveConnection()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        var tcs = Create.PendingHandshake(platform);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnConnectionResult("endpoint-1", Create.Resolution());

        // Assert
        var connection = await tcs.Task.WaitAsync(cts.Token);
        Assert.Equal("Alice", connection.RemoteDevice.DisplayName);
        Assert.True(platform._activeConnections.ContainsKey("endpoint-1"));
    }

    [Fact]
    public async Task Failure_FaultsConnectionTcs()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        var tcs = Create.PendingHandshake(platform);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnConnectionResult("endpoint-1", Create.Resolution(ConnectionsStatusCodes.StatusConnectionRejected));

        // Assert
        await Assert.ThrowsAsync<NearbyException>(() => tcs.Task.WaitAsync(cts.Token));
        Assert.False(platform._activeConnections.ContainsKey("endpoint-1"));
    }
}
