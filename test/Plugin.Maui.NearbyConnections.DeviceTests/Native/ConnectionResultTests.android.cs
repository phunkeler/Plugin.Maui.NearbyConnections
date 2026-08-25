namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The terminal "connection succeeded/failed" callback on Android (<c>OnConnectionResult</c>). The
/// repo's documented invariant: every failure path must resolve or fault the pending
/// <c>_connectionTcs</c> entry, or <c>AcceptAsync</c>/<c>ConnectAsync</c> hang forever.
/// </summary>
public class ConnectionResultTests : DeviceTest
{
    [Fact]
    public async Task Success_ResolvesConnectionTcsAndRegistersActiveConnection()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        var (tcs, deviceId) = Create.PendingHandshake(platform);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.AndroidAdapter.OnConnectionResult("endpoint-1", Create.Resolution());

        // Assert
        var connection = await tcs.Task.WaitAsync(cts.Token);
        Assert.Equal("Alice", connection.RemoteDevice.DisplayName);
        Assert.True(platform._activeConnections.ContainsKey(deviceId));
    }

    [Fact]
    public async Task Failure_FaultsConnectionTcs()
    {
        // Arrange
        await using var platform = Create.PlatformNearby();
        var (tcs, deviceId) = Create.PendingHandshake(platform);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.AndroidAdapter.OnConnectionResult("endpoint-1", Create.Resolution(ConnectionsStatusCodes.StatusConnectionRejected));

        // Assert
        await Assert.ThrowsAsync<NearbyException>(() => tcs.Task.WaitAsync(cts.Token));
        Assert.False(platform._activeConnections.ContainsKey(deviceId));
    }

    // A failed handshake drops the native endpoint, so the device must be reported lost too.
    // Without the lost event the session keeps showing a Visible row whose endpoint is already
    // gone, and tapping it can never connect.
    [Fact]
    public async Task Failure_PublishesLostEventAndReleasesPeer()
    {
        // Arrange — a discovered endpoint whose Found event is drained, then a failed handshake.
        await using var platform = Create.PlatformNearby();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        platform.AndroidAdapter.OnEndpointFound("endpoint-1", Create.DiscoveredEndpointInfo());
        var found = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        var (_, deviceId) = Create.PendingHandshake(platform);

        // Act
        platform.AndroidAdapter.OnConnectionResult("endpoint-1", Create.Resolution(ConnectionsStatusCodes.StatusConnectionRejected));

        // Assert
        var lost = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        Assert.False(lost.Found);
        Assert.Equal(found.Device.Id, lost.Device.Id);
        Assert.False(platform.PeerLookup.TryGetDevice(deviceId, out _));
    }
}
