namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The terminal "connection succeeded/failed" callback on iOS (<c>OnPeerStateChanged</c>). The
/// repo's documented invariant: every failure path must resolve or fault the pending
/// <c>_connectionTcs</c> entry, or <c>AcceptAsync</c>/<c>ConnectAsync</c> hang forever.
/// </summary>
public class ConnectionResultTests
{
    [Fact]
    public async Task Connected_ResolvesConnectionTcsAndRegistersActiveConnection()
    {
        // Arrange — Connected with NO prior Connecting: iOS does not guarantee that waypoint.
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        var (tcs, id) = Create.PendingHandshake(platform, peerId);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnPeerStateChanged(peerId, MCSessionState.Connected);

        // Assert
        var connection = await tcs.Task.WaitAsync(cts.Token);
        Assert.Equal("Alice", connection.RemoteDevice.DisplayName);
        Assert.True(platform._activeConnections.ContainsKey(id));
    }

    [Fact]
    public async Task NotConnected_FaultsConnectionTcs()
    {
        // Arrange — straight to NotConnected with no prior Connecting: the documented latent-hang path.
        await using var platform = Create.PlatformNearby();
        using var peerId = Create.PeerId("Alice");
        var (tcs, id) = Create.PendingHandshake(platform, peerId);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnPeerStateChanged(peerId, MCSessionState.NotConnected);

        // Assert
        await Assert.ThrowsAsync<NearbyException>(() => tcs.Task.WaitAsync(cts.Token));
        Assert.False(platform._activeConnections.ContainsKey(id));
    }
}
