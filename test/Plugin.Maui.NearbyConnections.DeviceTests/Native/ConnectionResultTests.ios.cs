namespace Plugin.Maui.NearbyConnections.DeviceTests.Native;

/// <summary>
/// The terminal "connection succeeded/failed" callback on iOS (<c>OnPeerStateChanged</c>). The
/// repo's documented invariant: every failure path must resolve or fault the pending
/// <c>_connectionTcs</c> entry, or <c>AcceptAsync</c>/<c>ConnectAsync</c> hang forever.
/// </summary>
public class ConnectionResultTests : DeviceTest
{
    [Fact]
    public async Task Connected_ResolvesConnectionTcsAndRegistersActiveConnection()
    {
        // Arrange — Connected with NO prior Connecting: iOS does not guarantee that waypoint.
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("Alice");
        var (tcs, id) = Create.PendingHandshake(platform, peerID);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnPeerStateChanged(peerID, MCSessionState.Connected);

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
        using var peerID = Create.PeerId("Alice");
        var (tcs, id) = Create.PendingHandshake(platform, peerID);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        platform.OnPeerStateChanged(peerID, MCSessionState.NotConnected);

        // Assert
        await Assert.ThrowsAsync<NearbyException>(() => tcs.Task.WaitAsync(cts.Token));
        Assert.False(platform._activeConnections.ContainsKey(id));
    }

    // A failed handshake drops the native peer handle, so the device must be reported lost too.
    // Without the lost event the session keeps showing a Visible row whose handle is already gone,
    // and tapping it can never connect.
    [Fact]
    public async Task NotConnected_PublishesLostEventAndReleasesPeer()
    {
        // Arrange — a discovered peer whose Found event is drained, then a failed handshake.
        await using var platform = Create.PlatformNearby();
        using var peerID = Create.PeerId("Alice");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        platform.FoundPeer(browser: null!, peerID: peerID, info: null);
        var found = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        var (_, id) = Create.PendingHandshake(platform, peerID);

        // Act
        platform.OnPeerStateChanged(peerID, MCSessionState.NotConnected);

        // Assert
        var lost = await platform._discoverChannel.Reader.ReadAsync(cts.Token);
        Assert.False(lost.Found);
        Assert.Equal(found.Device.Id, lost.Device.Id);
        Assert.False(platform.PeerLookup.TryGetDevice(id, out _));
    }
}
